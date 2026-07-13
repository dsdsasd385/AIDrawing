using System;
using System.Collections;
using System.Collections.Generic;
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

        // 워밍업 상태. 앱 실행당 1회만 예열하도록 가드한다
        private bool _warmedUp;
        private bool _warmingUp;

        // 워밍업 더미 이미지 크기(정사각). 내용은 무의미(결과 폐기) — 모델 적재만 목적이라 작게 잡아 샘플링을 줄인다
        private const int WarmupImageSize = 512;
        // 워밍업 재시도 (부팅 시 ComfyUI가 늦게 뜨는 경우 대비). 인수인계 §6 콜드 스타트 대응
        private const int WarmupMaxAttempts = 3;
        private const float WarmupRetryDelaySeconds = 5f;

        /// <summary>
        /// 서버를 예열한다. 앱 시작 시 1회 호출: 더미 생성으로 모델을 미리 적재해
        /// 콜드 스타트 첫 생성이 타임아웃을 넘겨 "첫 이미지가 안 나오는" 문제(인수인계 §6)를 없앤다.
        /// 스타일 전용 체크포인트(카툰 ToonYou 등)도 모두 예열한다 — 이걸 안 하면 해당 스타일
        /// 첫 생성이 2GB+ 콜드 디스크 로드로 타임아웃난다(카툰 생성 실패의 실제 원인, 2026-07-13).
        /// 결과는 버리며, 서버가 아직 안 떠 있으면 잠시 뒤 재시도한다. 관람객 체험을 막지 않는다(백그라운드).
        /// </summary>
        public void Warmup()
        {
            if (_warmedUp || _warmingUp) return;
            _warmingUp = true;
            StartCoroutine(WarmupRoutine());
        }

        private IEnumerator WarmupRoutine()
        {
            LogManager.Info("[ComfyUI] 워밍업 시작 (모델 예열)");
            byte[] dummy = MakeDummyPng();
            IReadOnlyList<StylePreset> styles = StyleLibrary.Styles;
            // 기본 체크포인트(워크플로 내장 = 실사) 스타일. 목록 폴백이 보장되므로 0번 사용
            StylePreset primary = styles[0];

            // 1) 서버 기동 감지 + 기본 체크포인트 예열 (부팅 시 서버가 늦게 뜨면 재시도)
            for (int attempt = 1; attempt <= WarmupMaxAttempts && !_warmedUp; attempt++)
            {
                bool done = false, ok = false;
                Generate("warmup", dummy, dummy, primary,
                    _ => { ok = true; done = true; },
                    _ => { done = true; });
                while (!done) yield return null;

                if (ok)
                {
                    _warmedUp = true;
                    LogManager.Info("[ComfyUI] 워밍업: 기본 체크포인트 예열 완료");
                }
                else if (attempt < WarmupMaxAttempts)
                {
                    // 대개 서버가 아직 준비 안 된 경우. 잠시 뒤 재시도
                    LogManager.Warn($"[ComfyUI] 워밍업 실패 {attempt}/{WarmupMaxAttempts} — {WarmupRetryDelaySeconds}초 후 재시도");
                    yield return new WaitForSecondsRealtime(WarmupRetryDelaySeconds);
                }
                else
                {
                    // 끝까지 실패해도 체험은 계속된다. 첫 실제 생성이 로딩을 감당(타임아웃 여유값이 보호)
                    LogManager.Warn("[ComfyUI] 워밍업 최종 실패 — 첫 실제 생성 때 모델이 적재된다");
                }
            }
            if (!_warmedUp) { _warmingUp = false; yield break; }

            // 2) 스타일 전용 체크포인트 예열. 한 번 적재하면 OS 파일 캐시에 남아 이후 스타일 전환 스왑이
            //    콜드 디스크 로드(수십 초) → 웜 스왑(~7초)으로 빨라진다. 8GB VRAM이라 상주는 하나뿐이지만
            //    목적은 VRAM 상주가 아니라 디스크 캐시 적재다.
            var warmed = new HashSet<string> { primary.checkpoint ?? "" };
            bool warmedExtra = false;
            foreach (StylePreset style in styles)
            {
                string ckpt = string.IsNullOrEmpty(style.checkpoint) ? "" : style.checkpoint;
                if (!warmed.Add(ckpt)) continue; // 이미 예열한 체크포인트는 건너뜀
                bool done = false;
                LogManager.Info($"[ComfyUI] 워밍업: 스타일 체크포인트 예열 ({ckpt})");
                Generate("warmup", dummy, dummy, style, _ => done = true, _ => done = true);
                while (!done) yield return null;
                warmedExtra = true;
            }
            // 전용 체크포인트를 예열했으면 VRAM 상주를 기본(실사)으로 되돌린다 — 가장 흔한 첫 선택이 스왑 없이 뜨도록
            if (warmedExtra)
            {
                bool done = false;
                Generate("warmup", dummy, dummy, primary, _ => done = true, _ => done = true);
                while (!done) yield return null;
            }
            LogManager.Info("[ComfyUI] 워밍업 완료 (모든 체크포인트 예열됨)");
            _warmingUp = false;
        }

        // 워밍업용 더미 PNG(흰 배경 + 가운데 검은 사각형). 파이프라인을 실제로 태워 모델을 적재하는 게 목적
        private static byte[] MakeDummyPng()
        {
            var tex = new Texture2D(WarmupImageSize, WarmupImageSize, TextureFormat.RGB24, false);
            var pixels = new Color32[WarmupImageSize * WarmupImageSize];
            var white = new Color32(255, 255, 255, 255);
            var black = new Color32(0, 0, 0, 255);
            int lo = WarmupImageSize / 4, hi = WarmupImageSize * 3 / 4;
            for (int y = 0; y < WarmupImageSize; y++)
                for (int x = 0; x < WarmupImageSize; x++)
                    pixels[y * WarmupImageSize + x] = (x >= lo && x < hi && y >= lo && y < hi) ? black : white;
            tex.SetPixels32(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Destroy(tex);
            return png;
        }

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
            string lineName = null, colorName = null, uploadError = null;
            yield return UploadImage(cfg, sessionId + "_line.png", linePng, (n, err) => { lineName = n; uploadError = err; });
            if (lineName == null) { Fail(onFailure, "선 레이어 업로드 실패: " + uploadError); yield break; }
            yield return UploadImage(cfg, sessionId + "_color.png", colorPng, (n, err) => { colorName = n; uploadError = err; });
            if (colorName == null) { Fail(onFailure, "색 레이어 업로드 실패: " + uploadError); yield break; }

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

        // onDone(서버가 저장한 파일명, 실패 상세). 실패 상세는 호출부가 Fail 메시지에 실어
        // AppFlow의 Error 로그에서도 근본 원인이 보이게 한다 (Warn까지 뒤지지 않아도 되도록)
        private IEnumerator UploadImage(ComfyUiConfig cfg, string fileName, byte[] png, Action<string, string> onDone)
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
                    // 접속 자체가 안 되는 경우가 압도적으로 흔한 원인(서버 미기동) — 진단 힌트를 함께 남긴다
                    string detail = $"{req.error} (HTTP {req.responseCode}, {cfg.baseUrl})";
                    if (!string.IsNullOrEmpty(req.error) && req.error.Contains("Cannot connect"))
                        detail += @" — ComfyUI 서버 미기동으로 보임. Tools\run_comfyui.bat 실행 여부 확인 (기동 후 준비까지 ~90초)";
                    LogManager.Warn($"[ComfyUI] 업로드 요청 실패({fileName}): {detail}");
                    onDone(null, detail);
                    yield break;
                }

                string savedName = ParseUploadedName(req.downloadHandler.text);
                onDone(savedName, savedName == null ? "업로드 응답 해석 실패 (서버 응답이 예상 JSON이 아님)" : null);
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
            workflow["4"]["inputs"]["text"] = style.negativePrompt;
            workflow["5"]["inputs"]["image"] = colorName;
            workflow["6"]["inputs"]["image"] = lineName;
            workflow["11"]["inputs"]["seed"] = UnityEngine.Random.Range(1, int.MaxValue); // 매회 다른 결과가 나오도록
            workflow["11"]["inputs"]["denoise"] = style.denoise;
            // 실사 모델로는 화풍이 안 나오는 스타일(카툰 등)만 전용 체크포인트로 교체. 스타일이 바뀌는 첫 생성은 모델 적재로 몇 초 느려진다
            if (!string.IsNullOrEmpty(style.checkpoint))
                workflow["1"]["inputs"]["ckpt_name"] = style.checkpoint;
            if (style.controlnetStrength > 0f)
                workflow["9"]["inputs"]["strength"] = style.controlnetStrength;

            // 스타일 전용 LoRA(픽셀아트 등): 체크포인트 위에 화풍 LoRA를 얹는다. 노드 20을 체크포인트와
            // 소비자(CLIP 3·4, KSampler 11) 사이에 끼워 model·clip을 LoRA 출력으로 돌린다
            string positive = style.prompt;
            if (!string.IsNullOrEmpty(style.lora))
            {
                workflow["20"] = new JObject
                {
                    ["class_type"] = "LoraLoader",
                    ["inputs"] = new JObject
                    {
                        ["model"] = new JArray { "1", 0 },
                        ["clip"] = new JArray { "1", 1 },
                        ["lora_name"] = style.lora,
                        ["strength_model"] = style.loraStrength,
                        ["strength_clip"] = style.loraStrength
                    }
                };
                workflow["3"]["inputs"]["clip"] = new JArray { "20", 1 };
                workflow["4"]["inputs"]["clip"] = new JArray { "20", 1 };
                workflow["11"]["inputs"]["model"] = new JArray { "20", 0 };
                if (!string.IsNullOrEmpty(style.loraTrigger))
                    positive = style.loraTrigger + ", " + positive;
            }
            workflow["3"]["inputs"]["text"] = positive;

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
            // 캐시버스터: 같은 URL을 반복 GET하면 UnityWebRequest/프록시가 "아직 결과 없음" 응답을 캐시해
            // 완료 후에도 옛 응답을 돌려주는 문제가 실측됨(콜드 스타트 첫 생성이 감지 안 됨, 인수인계 §6).
            // 매 폴링 URL에 타임스탬프를 붙이고 no-cache 헤더를 실어 항상 최신 상태를 받는다.
            string url = $"{cfg.baseUrl}/history/{promptId}?t={DateTime.UtcNow.Ticks}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Cache-Control", "no-cache");
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
