using System;
using System.Collections;
using System.IO;
using System.Text;
using CarDrawing.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace CarDrawing.Generation
{
    /// <summary>
    /// ComfyUI 서버(HTTP) 연동 클라이언트. 생성 시스템의 핵심이며 AppFlowManager가 호출한다.
    /// 흐름(계획서 7장): 스케치 2장 업로드 → 워크플로 JSON 치환 제출 → 완료 폴링 → 결과 PNG 다운로드.
    /// 어떤 단계가 실패해도 예외를 밖으로 던지지 않고 onFailure 콜백으로 알린다 (계획서 12장: 예외로 죽지 않기).
    /// </summary>
    public class ComfyUIClient : MonoBehaviour
    {
        // 개별 HTTP 요청 타임아웃(초). 로컬 서버라 즉시 응답이 정상이며, 전체 시간은 generateTimeoutSeconds가 제한한다
        private const int RequestTimeoutSeconds = 10;

        // 서버가 요청 주체를 구분하는 식별자. 앱 실행마다 새로 발급해도 무방
        private readonly string _clientId = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 스케치 한 쌍으로 이미지 생성을 요청한다.
        /// </summary>
        /// <param name="sessionId">세션 ID. 서버 업로드 파일명 충돌 방지에 쓴다</param>
        /// <param name="linePng">선 레이어 PNG (ControlNet Scribble 입력)</param>
        /// <param name="colorPng">색 레이어 PNG (img2img 초기 이미지)</param>
        /// <param name="style">적용할 스타일 (프롬프트 + 디노이즈)</param>
        /// <param name="onSuccess">결과 PNG 바이트를 받는 콜백</param>
        /// <param name="onFailure">실패 사유(로그용 한국어)를 받는 콜백</param>
        public void Generate(string sessionId, byte[] linePng, byte[] colorPng, StylePreset style,
            Action<byte[]> onSuccess, Action<string> onFailure)
        {
            StartCoroutine(GenerateRoutine(sessionId, linePng, colorPng, style, onSuccess, onFailure));
        }

        private IEnumerator GenerateRoutine(string sessionId, byte[] linePng, byte[] colorPng, StylePreset style,
            Action<byte[]> onSuccess, Action<string> onFailure)
        {
            ComfyUiConfig cfg = ConfigManager.Config.comfyUi;
            float deadline = Time.realtimeSinceStartup + cfg.generateTimeoutSeconds;

            // 1) 스케치 업로드. 세션 ID를 파일명에 넣어 이전 업로드와의 충돌을 피한다
            string lineName = null, colorName = null;
            yield return UploadImage(cfg, sessionId + "_line.png", linePng, n => lineName = n);
            if (lineName == null) { Fail(onFailure, "선 레이어 업로드 실패"); yield break; }
            yield return UploadImage(cfg, sessionId + "_color.png", colorPng, n => colorName = n);
            if (colorName == null) { Fail(onFailure, "색 레이어 업로드 실패"); yield break; }

            // 2) 워크플로 로드 + 치환 (노드 매핑은 인수인계 §5 — 워크플로 노드 ID 변경 시 여기도 수정)
            string payload = null, buildError = null;
            try { payload = BuildPromptPayload(cfg, lineName, colorName, style); }
            catch (Exception e) { buildError = e.Message; }
            if (payload == null) { Fail(onFailure, "워크플로 구성 실패: " + buildError); yield break; }

            // 3) 제출. 업로드 직후 첫 제출은 'Invalid image file'이 날 수 있어 재시도한다 (실측된 함정, 인수인계 §6)
            string promptId = null, submitError = null;
            for (int attempt = 0; attempt <= cfg.submitMaxRetries && promptId == null; attempt++)
            {
                if (attempt > 0) yield return new WaitForSecondsRealtime(0.3f);
                yield return SubmitPrompt(cfg, payload, (id, err) => { promptId = id; submitError = err; });
            }
            if (promptId == null) { Fail(onFailure, "워크플로 제출 실패: " + submitError); yield break; }

            // 4) 완료 폴링 (계획서 7장: 0.5초 간격, 전체 타임아웃 내)
            ResultLocation location = null;
            bool serverReportedError = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return PollHistory(cfg, promptId, (loc, err) => { location = loc; serverReportedError = err; });
                if (serverReportedError) { Fail(onFailure, "서버가 생성 오류를 보고함 (ComfyUI 콘솔 확인)"); yield break; }
                if (location != null) break;
                yield return new WaitForSecondsRealtime(cfg.pollIntervalSeconds);
            }
            if (location == null) { Fail(onFailure, $"생성 시간 초과 ({cfg.generateTimeoutSeconds}초)"); yield break; }

            // 5) 결과 다운로드
            byte[] resultPng = null;
            yield return DownloadResult(cfg, location, b => resultPng = b);
            if (resultPng == null) { Fail(onFailure, "결과 다운로드 실패"); yield break; }

            onSuccess?.Invoke(resultPng);
        }

        // 결과 파일의 서버상 위치 (/view 요청 파라미터 3종)
        private class ResultLocation
        {
            public string FileName;
            public string Subfolder;
            public string Type;
        }

        private static void Fail(Action<string> onFailure, string reason)
        {
            LogManager.Warn($"[ComfyUI] {reason}");
            onFailure?.Invoke(reason);
        }

        // ── 1) 업로드 ──────────────────────────────────────

        private IEnumerator UploadImage(ComfyUiConfig cfg, string fileName, byte[] png, Action<string> onDone)
        {
            var form = new WWWForm();
            form.AddBinaryData("image", png, fileName, "image/png");
            form.AddField("overwrite", "true");

            using (UnityWebRequest req = UnityWebRequest.Post(cfg.baseUrl + "/upload/image", form))
            {
                req.timeout = RequestTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    LogManager.Warn($"[ComfyUI] 업로드 요청 실패({fileName}): {req.error}");
                    onDone(null);
                    yield break;
                }
                onDone(ParseUploadedName(req.downloadHandler.text));
            }
        }

        private static string ParseUploadedName(string responseJson)
        {
            try
            {
                // 서버가 파일명을 바꿔 저장할 수 있으므로 응답의 name을 그대로 쓴다
                return (string)JObject.Parse(responseJson)["name"];
            }
            catch (Exception e)
            {
                LogManager.Warn($"[ComfyUI] 업로드 응답 해석 실패: {e.Message}");
                return null;
            }
        }

        // ── 2) 워크플로 치환 ────────────────────────────────

        private string BuildPromptPayload(ComfyUiConfig cfg, string lineName, string colorName, StylePreset style)
        {
            string path = Path.Combine(Application.streamingAssetsPath, cfg.workflowPath);
            JObject workflow = JObject.Parse(File.ReadAllText(path));

            // 인수인계 §5의 치환 표: 3=긍정, 4=부정, 5=색 레이어, 6=선 레이어, 11=seed/denoise
            workflow["3"]["inputs"]["text"] = style.prompt;
            workflow["4"]["inputs"]["text"] = style.negativePrompt;
            workflow["5"]["inputs"]["image"] = colorName;
            workflow["6"]["inputs"]["image"] = lineName;
            workflow["11"]["inputs"]["seed"] = UnityEngine.Random.Range(1, int.MaxValue); // 매회 다른 결과가 나오도록
            workflow["11"]["inputs"]["denoise"] = style.denoise;

            var payload = new JObject { ["prompt"] = workflow, ["client_id"] = _clientId };
            return payload.ToString(Formatting.None);
        }

        // ── 3) 제출 ────────────────────────────────────────

        private IEnumerator SubmitPrompt(ComfyUiConfig cfg, string payload, Action<string, string> onDone)
        {
            using (var req = new UnityWebRequest(cfg.baseUrl + "/prompt", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = RequestTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    // 본문에 검증 실패 사유(예: Invalid image file)가 담기므로 로그에 포함한다
                    string detail = $"{req.error} / {req.downloadHandler.text}";
                    LogManager.Warn($"[ComfyUI] 제출 실패: {detail}");
                    onDone(null, detail);
                    yield break;
                }
                onDone(ParsePromptId(req.downloadHandler.text), null);
            }
        }

        private static string ParsePromptId(string responseJson)
        {
            try
            {
                return (string)JObject.Parse(responseJson)["prompt_id"];
            }
            catch (Exception e)
            {
                LogManager.Warn($"[ComfyUI] 제출 응답 해석 실패: {e.Message}");
                return null;
            }
        }

        // ── 4) 폴링 ────────────────────────────────────────

        private IEnumerator PollHistory(ComfyUiConfig cfg, string promptId, Action<ResultLocation, bool> onDone)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(cfg.baseUrl + "/history/" + promptId))
            {
                req.timeout = RequestTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    // 폴링 1회 실패는 치명적이지 않다 — 다음 주기에 재시도 (전체 타임아웃이 상한)
                    LogManager.Warn($"[ComfyUI] 히스토리 조회 실패: {req.error}");
                    onDone(null, false);
                    yield break;
                }
                ParseHistory(req.downloadHandler.text, promptId, onDone);
            }
        }

        private static void ParseHistory(string responseJson, string promptId, Action<ResultLocation, bool> onDone)
        {
            try
            {
                // 완료 전에는 응답에 promptId 항목이 아직 없다 — 정상 (계속 폴링)
                if (!(JObject.Parse(responseJson)[promptId] is JObject entry))
                {
                    onDone(null, false);
                    return;
                }

                // 서버가 실행 오류를 보고했는지 확인 (모델 없음, VRAM 부족 등)
                if ((string)entry.SelectToken("status.status_str") == "error")
                {
                    onDone(null, true);
                    return;
                }

                // outputs에서 images를 가진 첫 노드(SaveImage)를 찾는다 — 출력 노드 ID 하드코딩 회피
                if (entry["outputs"] is JObject outputs)
                {
                    foreach (JProperty node in outputs.Properties())
                    {
                        if (!(node.Value["images"] is JArray images) || images.Count == 0) continue;
                        JToken img = images[0];
                        onDone(new ResultLocation
                        {
                            FileName = (string)img["filename"],
                            Subfolder = (string)img["subfolder"],
                            Type = (string)img["type"]
                        }, false);
                        return;
                    }
                }
                onDone(null, false);
            }
            catch (Exception e)
            {
                LogManager.Warn($"[ComfyUI] 히스토리 해석 실패: {e.Message}");
                onDone(null, false);
            }
        }

        // ── 5) 다운로드 ─────────────────────────────────────

        private IEnumerator DownloadResult(ComfyUiConfig cfg, ResultLocation location, Action<byte[]> onDone)
        {
            string url = $"{cfg.baseUrl}/view" +
                         $"?filename={UnityWebRequest.EscapeURL(location.FileName)}" +
                         $"&subfolder={UnityWebRequest.EscapeURL(location.Subfolder ?? string.Empty)}" +
                         $"&type={UnityWebRequest.EscapeURL(string.IsNullOrEmpty(location.Type) ? "output" : location.Type)}";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = RequestTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    LogManager.Warn($"[ComfyUI] 결과 다운로드 실패: {req.error}");
                    onDone(null);
                    yield break;
                }
                onDone(req.downloadHandler.data);
            }
        }
    }
}
