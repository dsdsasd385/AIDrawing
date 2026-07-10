using System;
using System.Collections;
using System.IO;
using System.Text;
using CarDrawing.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace CarDrawing.Results
{
    /// <summary>
    /// 갤러리 게이트용 자동 필터 (계획서 10장). 결과 처리 시스템에 속하며
    /// AppFlowManager가 관람객의 [전시장에 내 작품 걸기] 신청(opt-in)을 받으면 호출한다.
    /// 로컬 VLM 서버(OpenAI 호환 chat completions — Ollama·llama.cpp 등)에 스케치와 결과를 보여주고
    /// 전시 가능 여부를 판정받는다. GPU는 SD가 점유하므로 VLM은 CPU 서버 전제 (계획서 2장).
    /// 판정 정책은 보수적: 필터가 꺼져 있으면 통과, 켜져 있는데 판정 불가(서버 없음·타임아웃·응답 해석 실패)면
    /// 격리(Quarantine) — 본인 화면·QR은 정상 제공되므로 관람객 피해가 없다 (계획서 10장).
    /// </summary>
    public class ContentFilter : MonoBehaviour
    {
        /// <summary>
        /// 세션의 스케치·결과 이미지를 검사한다. 관람객은 결과를 기다리지 않는다(백그라운드).
        /// </summary>
        /// <param name="sessionId">검사할 세션 ID (Sessions 폴더의 파일을 읽는다)</param>
        /// <param name="onDone">true=갤러리 등재 가능, false=격리 대상</param>
        public void Evaluate(string sessionId, Action<bool> onDone)
        {
            FilterConfig cfg = ConfigManager.Config.filter;
            if (!cfg.enabled)
            {
                // 필터를 끈 운영 상태 — opt-in 작품이 곧장 갤러리로 간다 (Config.json filter.enabled)
                onDone?.Invoke(true);
                return;
            }
            StartCoroutine(EvaluateRoutine(sessionId, cfg, onDone));
        }

        private IEnumerator EvaluateRoutine(string sessionId, FilterConfig cfg, Action<bool> onDone)
        {
            string payload = null;
            try { payload = BuildPayload(sessionId, cfg); }
            catch (Exception e) { LogManager.Warn($"[Filter] 요청 구성 실패 (세션 {sessionId}): {e.Message}"); }
            if (payload == null)
            {
                onDone?.Invoke(false); // 이미지가 없거나 읽기 실패 — 보수적으로 격리
                yield break;
            }

            using (var req = new UnityWebRequest(cfg.endpoint, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = (int)cfg.timeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    // VLM 서버 부재·타임아웃 — 판정 불가는 격리 (계획서 10장: 애매하면 제외)
                    LogManager.Warn($"[Filter] 판정 요청 실패 (세션 {sessionId}): {req.error}");
                    onDone?.Invoke(false);
                    yield break;
                }
                onDone?.Invoke(ParseVerdict(req.downloadHandler.text, sessionId));
            }
        }

        // 스케치+결과 2장을 data URI로 실어 OpenAI 호환 chat completions 요청을 만든다.
        // 사진 학습 NSFW 분류기 대신 VLM에 질문하는 이유: 낙서/선화에서 분류기가 오동작한다 (계획서 10장)
        private static string BuildPayload(string sessionId, FilterConfig cfg)
        {
            byte[] sketch = File.ReadAllBytes(SessionStore.SketchPath(sessionId));
            byte[] result = File.ReadAllBytes(SessionStore.ResultPath(sessionId));

            var payload = new JObject
            {
                ["model"] = cfg.model,
                ["max_tokens"] = 100, // 판정은 JSON 한 줄이면 충분 — 장문 생성으로 CPU 시간을 낭비하지 않는다
                ["messages"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = new JArray
                        {
                            new JObject { ["type"] = "text", ["text"] = cfg.question },
                            ImagePart(sketch),
                            ImagePart(result)
                        }
                    }
                }
            };
            return payload.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static JObject ImagePart(byte[] png) => new JObject
        {
            ["type"] = "image_url",
            ["image_url"] = new JObject { ["url"] = "data:image/png;base64," + Convert.ToBase64String(png) }
        };

        // 응답에서 {"ok": true/false}를 찾는다. 모델이 JSON 앞뒤에 말을 붙이는 경우가 흔해 중괄호 구간만 잘라 해석한다
        private static bool ParseVerdict(string responseJson, string sessionId)
        {
            try
            {
                string content = (string)JObject.Parse(responseJson).SelectToken("choices[0].message.content");
                if (string.IsNullOrEmpty(content)) throw new Exception("응답 content 없음");

                int start = content.IndexOf('{');
                int end = content.LastIndexOf('}');
                if (start < 0 || end <= start) throw new Exception("응답에 JSON 없음: " + content);

                bool ok = (bool?)JObject.Parse(content.Substring(start, end - start + 1))["ok"] ?? false;
                LogManager.Info($"[Filter] 판정 완료 (세션 {sessionId}): {(ok ? "통과" : "부적합")}");
                return ok;
            }
            catch (Exception e)
            {
                // 해석 불가도 판정 불가로 취급 — 보수적 격리
                LogManager.Warn($"[Filter] 판정 해석 실패 (세션 {sessionId}): {e.Message}");
                return false;
            }
        }
    }
}
