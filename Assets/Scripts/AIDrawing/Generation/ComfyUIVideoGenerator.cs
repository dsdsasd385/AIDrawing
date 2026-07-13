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
    /// 로컬 ComfyUI(AnimateDiff)로 결과 이미지를 짧은 영상(mp4)으로 만드는 IVideoGenerator 구현 (마일스톤 ⑥).
    /// 흐름은 ComfyUIClient와 동일(업로드 → 워크플로 치환 제출 → 폴링 → 다운로드)하지만
    /// 워크플로·타임아웃·결과 종류(mp4, VHS 노드는 outputs.gifs에 실림)가 달라 별도 클래스로 둔다.
    /// 영상 워크플로는 노드 ID 101번대를 쓴다 — 이미지 워크플로와 ID가 겹치면
    /// ComfyUI 프롬프트 간 캐시 공유로 생성이 붕괴한다 (인수인계 §6, 2026-07-13 실측 함정).
    /// </summary>
    public class ComfyUIVideoGenerator : MonoBehaviour, IVideoGenerator
    {
        // 개별 HTTP 요청 타임아웃(초). 전체 시간은 video.generateTimeoutSeconds가 제한한다
        private const int RequestTimeoutSeconds = 10;

        // 워크플로 치환 대상 노드 ID (car_video_workflow_api.json — 바꾸면 여기도 수정, 인수인계 §5 규칙과 동일)
        private const string NodeResultImage = "107"; // LoadImage: 영상의 기반이 되는 결과 이미지
        private const string NodeSampler = "110";     // KSampler: seed
        private const string NodeLineImage = "114";   // LoadImage: 형태 고정용 선 레이어 (ControlNet)
        private const string NodeVideoDecode = "111"; // VAEDecode: 픽셀화 주입 시 프레임 소스
        private const string NodeVideoScale = "113";  // ImageScale: 영상 프레임 크기 기준 (픽셀 그리드 계산용)
        private const string NodeVideoCombine = "112"; // VHS_VideoCombine: 픽셀화 주입 시 입력을 여기로 돌린다
        private const string NodeCheckpoint = "101";  // CheckpointLoaderSimple: LoRA 주입 시 소스
        private const string NodeAnimateDiff = "104"; // ADE 로더: LoRA 주입 시 model 입력을 LoRA로 돌린다
        private const string NodePositive = "105";    // CLIPTextEncode(긍정): LoRA 트리거·clip 재연결
        private const string NodeNegative = "106";    // CLIPTextEncode(부정): clip 재연결

        private readonly string _clientId = Guid.NewGuid().ToString("N");

        /// <summary>Config의 video.enabled를 그대로 따른다. 워크플로 파일이 없으면 첫 시도가 실패 콜백으로 알린다</summary>
        public bool IsEnabled => ConfigManager.Config.video.enabled;

        /// <inheritdoc/>
        public void Generate(string sessionId, byte[] resultPng, byte[] linePng, StylePreset style,
            Action<byte[]> onSuccess, Action<string> onFailure)
        {
            StartCoroutine(GenerateRoutine(sessionId, resultPng, linePng, style, onSuccess, onFailure));
        }

        private IEnumerator GenerateRoutine(string sessionId, byte[] resultPng, byte[] linePng, StylePreset style,
            Action<byte[]> onSuccess, Action<string> onFailure)
        {
            ComfyUiConfig server = ConfigManager.Config.comfyUi;
            VideoConfig video = ConfigManager.Config.video;
            float deadline = Time.realtimeSinceStartup + video.generateTimeoutSeconds;

            // 1) 입력 업로드. 이미지 생성 때 올린 파일과 이름이 겹치지 않게 접두사를 달리한다
            string resultName = null, lineName = null, uploadError = null;
            yield return UploadImage(server, sessionId + "_vresult.png", resultPng,
                (n, err) => { resultName = n; uploadError = err; });
            if (resultName == null) { Fail(onFailure, "영상 기반 이미지 업로드 실패: " + uploadError); yield break; }
            yield return UploadImage(server, sessionId + "_vline.png", linePng,
                (n, err) => { lineName = n; uploadError = err; });
            if (lineName == null) { Fail(onFailure, "영상 선 레이어 업로드 실패: " + uploadError); yield break; }

            // 2) 영상 워크플로 로드 + 치환
            string payload = null, buildError = null;
            try { payload = BuildPromptPayload(video, resultName, lineName, style); }
            catch (Exception e) { buildError = e.Message; }
            if (payload == null) { Fail(onFailure, "영상 워크플로 구성 실패: " + buildError); yield break; }

            // 3) 제출 (업로드 직후 첫 제출 실패 함정은 이미지와 동일 — 재시도 1회)
            string promptId = null, submitError = null;
            for (int attempt = 0; attempt <= server.submitMaxRetries && promptId == null; attempt++)
            {
                if (attempt > 0) yield return new WaitForSecondsRealtime(0.3f);
                yield return SubmitPrompt(server, payload, (id, err) => { promptId = id; submitError = err; });
            }
            if (promptId == null) { Fail(onFailure, "영상 워크플로 제출 실패: " + submitError); yield break; }

            // 4) 완료 폴링. 영상은 40초 이상 걸리므로 이미지보다 느슨한 1초 간격이면 충분하다
            ResultLocation location = null;
            bool serverReportedError = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return PollHistory(server, promptId, (loc, err) => { location = loc; serverReportedError = err; });
                if (serverReportedError) { Fail(onFailure, "서버가 영상 생성 오류를 보고함 (ComfyUI 콘솔 확인)"); yield break; }
                if (location != null) break;
                yield return new WaitForSecondsRealtime(1f);
            }
            if (location == null) { Fail(onFailure, $"영상 생성 시간 초과 ({video.generateTimeoutSeconds}초)"); yield break; }

            // 5) mp4 다운로드
            byte[] mp4 = null;
            yield return DownloadResult(server, location, b => mp4 = b);
            if (mp4 == null) { Fail(onFailure, "영상 다운로드 실패"); yield break; }

            onSuccess?.Invoke(mp4);
        }

        private class ResultLocation
        {
            public string FileName;
            public string Subfolder;
            public string Type;
        }

        private static void Fail(Action<string> onFailure, string reason)
        {
            LogManager.Warn($"[ComfyUI영상] {reason}");
            onFailure?.Invoke(reason);
        }

        // ── 업로드 (ComfyUIClient와 동일 엔드포인트) ─────────────

        private IEnumerator UploadImage(ComfyUiConfig server, string fileName, byte[] png, Action<string, string> onDone)
        {
            var form = new WWWForm();
            form.AddBinaryData("image", png, fileName, "image/png");
            form.AddField("overwrite", "true");

            using (UnityWebRequest req = UnityWebRequest.Post(server.baseUrl + "/upload/image", form))
            {
                req.timeout = RequestTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onDone(null, $"{req.error} (HTTP {req.responseCode})");
                    yield break;
                }
                string savedName = null;
                try { savedName = (string)JObject.Parse(req.downloadHandler.text)["name"]; }
                catch (Exception e) { LogManager.Warn($"[ComfyUI영상] 업로드 응답 해석 실패: {e.Message}"); }
                onDone(savedName, savedName == null ? "업로드 응답 해석 실패" : null);
            }
        }

        // ── 워크플로 치환 ────────────────────────────────────

        private string BuildPromptPayload(VideoConfig video, string resultName, string lineName, StylePreset style)
        {
            string path = Path.Combine(Application.streamingAssetsPath, video.workflowPath);
            JObject workflow = JObject.Parse(File.ReadAllText(path));

            workflow[NodeResultImage]["inputs"]["image"] = resultName;
            workflow[NodeLineImage]["inputs"]["image"] = lineName;
            workflow[NodeSampler]["inputs"]["seed"] = UnityEngine.Random.Range(1, int.MaxValue);

            // 스타일 전용 LoRA(픽셀아트 등)는 영상에도 얹는다 — 안 얹으면 RV가 화풍을 잃고 매끈하게 다시 그린다.
            // 픽셀화만으로는 영상 픽셀 품질이 거칠어져서, LoRA로 화풍을 유지한 뒤 픽셀화로 그리드만 스냅한다 (인수인계 §6)
            if (style != null && !string.IsNullOrEmpty(style.lora))
                InjectLora(workflow, style);

            // 픽셀아트 스타일: 영상 프레임도 그리드에 스냅한다. 안 하면 AnimateDiff가 프레임을 매끈하게 재해석해 픽셀감이 사라진다
            if (style != null && style.pixelateWidth > 0)
                InjectPixelate(workflow, style.pixelateWidth);

            var payload = new JObject { ["prompt"] = workflow, ["client_id"] = _clientId };
            return payload.ToString(Formatting.None);
        }

        // 영상 워크플로에 화풍 LoRA를 주입한다: 체크포인트(101) → LoRA(123) → AnimateDiff(104)·CLIP(105·106).
        // 트리거 단어를 긍정 프롬프트 앞에 붙여 LoRA가 확실히 발현되게 한다 (이미지 생성부와 같은 방식)
        private static void InjectLora(JObject workflow, StylePreset style)
        {
            workflow["123"] = new JObject
            {
                ["class_type"] = "LoraLoader",
                ["inputs"] = new JObject
                {
                    ["model"] = new JArray { NodeCheckpoint, 0 },
                    ["clip"] = new JArray { NodeCheckpoint, 1 },
                    ["lora_name"] = style.lora,
                    ["strength_model"] = style.loraStrength,
                    ["strength_clip"] = style.loraStrength
                }
            };
            workflow[NodeAnimateDiff]["inputs"]["model"] = new JArray { "123", 0 };
            workflow[NodePositive]["inputs"]["clip"] = new JArray { "123", 1 };
            workflow[NodeNegative]["inputs"]["clip"] = new JArray { "123", 1 };
            if (!string.IsNullOrEmpty(style.loraTrigger))
                workflow[NodePositive]["inputs"]["text"] =
                    style.loraTrigger + ", " + (string)workflow[NodePositive]["inputs"]["text"];
        }

        // 디코드된 프레임(111)을 니어리스트로 축소→확대해 픽셀 그리드를 복원하고, VHS 입력을 그 결과로 돌린다.
        // 이미지 후처리 PixelArtFilter와 같은 원리지만 mp4 프레임에는 ComfyUI 노드로 적용하는 게 간단하다
        private static void InjectPixelate(JObject workflow, int pixelateWidth)
        {
            // 영상 프레임 크기(ImageScale 노드 113) 기준으로 그리드 계산 — 못 읽으면 기본 512×344
            int w = 512, h = 344;
            try
            {
                w = (int)workflow[NodeVideoScale]["inputs"]["width"];
                h = (int)workflow[NodeVideoScale]["inputs"]["height"];
            }
            catch { /* 워크플로가 바뀌어 못 읽으면 기본값 사용 */ }

            int gw = Mathf.Min(pixelateWidth, w);
            int gh = Mathf.Max(1, Mathf.RoundToInt((float)h * gw / w));

            workflow["121"] = new JObject
            {
                ["class_type"] = "ImageScale",
                ["inputs"] = new JObject
                {
                    ["image"] = new JArray { NodeVideoDecode, 0 },
                    ["upscale_method"] = "nearest-exact",
                    ["width"] = gw, ["height"] = gh, ["crop"] = "disabled"
                }
            };
            workflow["122"] = new JObject
            {
                ["class_type"] = "ImageScale",
                ["inputs"] = new JObject
                {
                    ["image"] = new JArray { "121", 0 },
                    ["upscale_method"] = "nearest-exact",
                    ["width"] = w, ["height"] = h, ["crop"] = "disabled"
                }
            };
            workflow[NodeVideoCombine]["inputs"]["images"] = new JArray { "122", 0 };
        }

        // ── 제출 ────────────────────────────────────────────

        private IEnumerator SubmitPrompt(ComfyUiConfig server, string payload, Action<string, string> onDone)
        {
            using (var req = new UnityWebRequest(server.baseUrl + "/prompt", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = RequestTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onDone(null, $"{req.error} / {req.downloadHandler.text}");
                    yield break;
                }
                string promptId = null;
                try { promptId = (string)JObject.Parse(req.downloadHandler.text)["prompt_id"]; }
                catch (Exception e) { LogManager.Warn($"[ComfyUI영상] 제출 응답 해석 실패: {e.Message}"); }
                onDone(promptId, promptId == null ? "제출 응답 해석 실패" : null);
            }
        }

        // ── 폴링 ────────────────────────────────────────────

        private IEnumerator PollHistory(ComfyUiConfig server, string promptId, Action<ResultLocation, bool> onDone)
        {
            // 캐시버스터 + no-cache: 같은 URL 반복 GET의 응답 캐싱으로 완료를 못 보는 함정 대응 (인수인계 §6)
            string url = $"{server.baseUrl}/history/{promptId}?t={DateTime.UtcNow.Ticks}";
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Cache-Control", "no-cache");
                req.timeout = RequestTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    // 폴링 1회 실패는 치명적이지 않다 — 다음 주기에 재시도 (전체 타임아웃이 상한)
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
                if (!(JObject.Parse(responseJson)[promptId] is JObject entry))
                {
                    onDone(null, false); // 완료 전에는 항목이 없다 — 계속 폴링
                    return;
                }

                if ((string)entry.SelectToken("status.status_str") == "error")
                {
                    onDone(null, true);
                    return;
                }

                // VHS_VideoCombine은 영상 파일을 outputs의 "gifs" 배열로 보고한다 (mp4여도 키 이름은 gifs).
                // 노드 ID 하드코딩을 피해 gifs를 가진 첫 노드를 찾는다
                if (entry["outputs"] is JObject outputs)
                {
                    foreach (JProperty node in outputs.Properties())
                    {
                        if (!(node.Value["gifs"] is JArray videos) || videos.Count == 0) continue;
                        JToken v = videos[0];
                        onDone(new ResultLocation
                        {
                            FileName = (string)v["filename"],
                            Subfolder = (string)v["subfolder"],
                            Type = (string)v["type"]
                        }, false);
                        return;
                    }
                }
                onDone(null, false);
            }
            catch (Exception e)
            {
                LogManager.Warn($"[ComfyUI영상] 히스토리 해석 실패: {e.Message}");
                onDone(null, false);
            }
        }

        // ── 다운로드 ─────────────────────────────────────────

        private IEnumerator DownloadResult(ComfyUiConfig server, ResultLocation location, Action<byte[]> onDone)
        {
            string url = $"{server.baseUrl}/view" +
                         $"?filename={UnityWebRequest.EscapeURL(location.FileName)}" +
                         $"&subfolder={UnityWebRequest.EscapeURL(location.Subfolder ?? string.Empty)}" +
                         $"&type={UnityWebRequest.EscapeURL(string.IsNullOrEmpty(location.Type) ? "output" : location.Type)}";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = RequestTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    LogManager.Warn($"[ComfyUI영상] 결과 다운로드 실패: {req.error}");
                    onDone(null);
                    yield break;
                }
                onDone(req.downloadHandler.data);
            }
        }
    }
}
