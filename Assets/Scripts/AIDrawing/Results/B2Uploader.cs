using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CarDrawing.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace CarDrawing.Results
{
    /// <summary>
    /// Backblaze B2 업로더 — IResultUploader의 기본 구현 (계획서 9-2, 2026-07-10 GCS에서 전환).
    /// 선택 이유: 카드 등록 없이 10GB 영구 무료 + 토큰 방식의 단순한 인증.
    /// 버킷은 **비공개**로 운영한다 — B2는 공개 버킷에만 카드 등록을 요구하므로, QR 링크는
    /// b2_get_download_authorization의 만료형 토큰(기본 7일)을 붙여 만든다.
    /// 흐름: b2_authorize_account(키 파일) → b2_get_upload_url → PNG 업로드 → 다운로드 토큰 →
    /// 저장 버튼이 있는 랜딩 HTML 업로드 → QR은 랜딩 페이지를 가리킨다 (같은 접두사라 토큰 1개로 커버).
    /// 키 파일이 없으면 IsConfigured=false로 QR 기능 전체가 조용히 꺼진다 (GcsUploader와 동일 계약).
    /// AppFlowManager가 B2 → GCS 순으로 설정된 업로더를 고른다.
    /// </summary>
    public class B2Uploader : MonoBehaviour, IResultUploader
    {
        private const string AuthorizeUrl = "https://api.backblazeb2.com/b2api/v2/b2_authorize_account";
        // B2 인증 토큰은 24시간 유효 — 여유를 두고 재발급한다 (장시간 무인 운영 중 만료 경계 회피)
        private const float AuthLifetimeSeconds = 23f * 3600f;

        // 키 파일(Config/b2-key.json) 값. 버킷 제한 키면 bucketId/bucketName은 authorize 응답이 채워준다
        private string _keyId;
        private string _applicationKey;
        private string _keyFileBucketId;
        private string _keyFileBucketName;
        private bool _credentialsLoaded;
        private bool _credentialsFailed;

        // b2_authorize_account 응답 캐시
        private string _apiUrl;
        private string _authToken;
        private string _downloadUrl;
        private string _bucketId;
        private string _bucketName;
        private float _authExpiresAt;

        /// <summary>키 파일이 있어 업로드가 가능한지. false면 QR 기능을 통째로 숨긴다</summary>
        public bool IsConfigured
        {
            get
            {
                LoadCredentials();
                return !_credentialsFailed;
            }
        }

        /// <summary>
        /// 결과 PNG를 B2 공개 버킷에 비동기 업로드한다. 실패 시 콜백에 null — 호출부는 QR만 숨긴다.
        /// </summary>
        /// <param name="sessionId">세션 ID (객체 이름에 포함)</param>
        /// <param name="png">결과 PNG 바이트</param>
        /// <param name="onDone">공개 다운로드 URL 콜백 (실패 시 null)</param>
        public void Upload(string sessionId, byte[] png, Action<string> onDone)
        {
            if (!IsConfigured)
            {
                onDone?.Invoke(null);
                return;
            }
            StartCoroutine(UploadRoutine(sessionId, png, onDone));
        }

        // ── 자격 증명 로드 ──────────────────────────────────

        private void LoadCredentials()
        {
            if (_credentialsLoaded) return;
            _credentialsLoaded = true;
            _credentialsFailed = true; // 성공 시에만 해제

            try
            {
                string path = ResolveKeyPath(ConfigManager.Config.b2.keyFilePath);
                if (path == null || !File.Exists(path))
                {
                    // 키 미배치는 정상 운영 상태일 수 있다(오프라인 전시) — 경고 1회만 남기고 QR을 끈다
                    LogManager.Warn($"[B2] 키 파일 없음 — QR 기능 비활성: {path}");
                    return;
                }

                JObject key = JObject.Parse(File.ReadAllText(path));
                _keyId = (string)key["keyId"];
                _applicationKey = (string)key["applicationKey"];
                // 버킷 제한이 없는 키를 쓰는 경우를 위한 선택 항목 (제한 키면 authorize 응답의 allowed가 우선)
                _keyFileBucketId = (string)key["bucketId"];
                _keyFileBucketName = (string)key["bucketName"];

                if (string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_applicationKey))
                {
                    LogManager.Warn("[B2] 키 파일에 keyId/applicationKey 없음 — QR 기능 비활성");
                    return;
                }

                _credentialsFailed = false;
                LogManager.Info($"[B2] 자격 증명 로드 완료: keyId {_keyId}");
            }
            catch (Exception e)
            {
                LogManager.Warn($"[B2] 자격 증명 로드 실패 — QR 기능 비활성: {e.Message}");
            }
        }

        // 키 파일 경로 해석. 상대 경로는 exe 옆(에디터에서는 프로젝트 루트) 기준 —
        // 키를 저장소·StreamingAssets에 두지 않기 위한 구조 (인수인계 §7, GcsUploader와 동일)
        private static string ResolveKeyPath(string configured)
        {
            if (string.IsNullOrEmpty(configured)) return null;
            if (Path.IsPathRooted(configured)) return configured;
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, configured);
        }

        // ── 1) 계정 인증 (b2_authorize_account) ───────────

        private IEnumerator EnsureAuth(Action<bool> onDone)
        {
            if (_authToken != null && Time.realtimeSinceStartup < _authExpiresAt)
            {
                onDone(true);
                yield break;
            }

            using (UnityWebRequest req = UnityWebRequest.Get(AuthorizeUrl))
            {
                string basic = Convert.ToBase64String(Encoding.ASCII.GetBytes(_keyId + ":" + _applicationKey));
                req.SetRequestHeader("Authorization", "Basic " + basic);
                req.timeout = (int)ConfigManager.Config.b2.uploadTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    LogManager.Warn($"[B2] 계정 인증 실패: {req.error} / {req.downloadHandler.text}");
                    onDone(false);
                    yield break;
                }

                try
                {
                    JObject res = JObject.Parse(req.downloadHandler.text);
                    _apiUrl = (string)res["apiUrl"];
                    _authToken = (string)res["authorizationToken"];
                    _downloadUrl = (string)res["downloadUrl"];
                    // 버킷 제한 키(권장 — 최소 권한)면 allowed에 버킷 정보가 들어온다. 없으면 키 파일 값 사용
                    _bucketId = (string)res.SelectToken("allowed.bucketId") ?? _keyFileBucketId;
                    _bucketName = (string)res.SelectToken("allowed.bucketName") ?? _keyFileBucketName;
                    _authExpiresAt = Time.realtimeSinceStartup + AuthLifetimeSeconds;

                    if (string.IsNullOrEmpty(_bucketId) || string.IsNullOrEmpty(_bucketName))
                    {
                        // 전체 권한 키 + 키 파일에 버킷 정보 누락 — 어디로 올릴지 알 수 없다
                        LogManager.Warn("[B2] 버킷 미지정 — 버킷 제한 키를 쓰거나 키 파일에 bucketId/bucketName을 넣을 것");
                        _authToken = null;
                        onDone(false);
                        yield break;
                    }
                }
                catch (Exception e)
                {
                    LogManager.Warn($"[B2] 인증 응답 해석 실패: {e.Message}");
                    _authToken = null;
                    onDone(false);
                    yield break;
                }
                onDone(true);
            }
        }

        // ── 2~3) 업로드 URL 발급 + 업로드 ─────────────────

        private IEnumerator UploadRoutine(string sessionId, byte[] png, Action<string> onDone)
        {
            B2Config cfg = ConfigManager.Config.b2;
            // 세션 ID는 시각 기반이라 추측 가능 — 타인 작품 URL을 유추하지 못하도록 난수 접미사.
            // PNG와 랜딩 HTML이 같은 접두사를 공유해 다운로드 토큰 1개(fileNamePrefix)로 둘 다 커버된다
            string baseName = cfg.objectPrefix + sessionId + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string pngName = baseName + ".png";
            string htmlName = baseName + ".html";

            // 토큰 만료(24h) 직후 첫 업로드가 401로 실패할 수 있어 인증 무효화 후 1회 재시도한다
            for (int attempt = 0; attempt < 2; attempt++)
            {
                bool authOk = false;
                yield return EnsureAuth(ok => authOk = ok);
                if (!authOk)
                {
                    onDone?.Invoke(null);
                    yield break;
                }

                // 업로드 URL 발급 (B2는 업로드 전용 URL·토큰을 따로 쓴다. 오류가 나기 전까지 재사용 가능)
                string uploadUrl = null, uploadToken = null;
                bool authExpired = false;
                yield return GetUploadUrl(cfg, (url, token, expired) => { uploadUrl = url; uploadToken = token; authExpired = expired; });
                if (uploadUrl == null)
                {
                    if (authExpired && attempt == 0) { _authToken = null; continue; }
                    onDone?.Invoke(null);
                    yield break;
                }

                // 1) 결과 PNG 업로드
                bool uploaded = false;
                yield return PutFile(cfg, uploadUrl, uploadToken, pngName, png, "image/png", ok => uploaded = ok);
                if (!uploaded)
                {
                    onDone?.Invoke(null);
                    yield break;
                }

                // 2) 다운로드 토큰 (비공개 버킷 운영 — 공개 버킷은 카드 등록이 필요해서 이 방식이 기본)
                string authQuery = null;
                if (cfg.downloadAuthSeconds > 0)
                {
                    yield return GetDownloadAuth(cfg, baseName, t => authQuery = t);
                    if (authQuery == null)
                    {
                        onDone?.Invoke(null); // 토큰 없이 만든 링크는 죽은 링크 — QR을 숨기는 쪽이 낫다
                        yield break;
                    }
                }
                // 이미지 주소는 랜딩 페이지와 같은 폴더라 상대 경로면 충분 (HTML을 짧게 유지)
                string tokenQuery = authQuery == null ? "" : "?Authorization=" + authQuery;
                string imageRelative = Path.GetFileName(pngName) + tokenQuery;

                // 3) 저장 버튼이 있는 랜딩 HTML 업로드 (계획서 9-2). 실패하면 PNG 직링크로 폴백 — QR은 계속 산다
                string landingHtml = LoadLandingTemplate(cfg).Replace("{{IMAGE_URL}}", imageRelative);
                bool htmlUploaded = false;
                yield return PutFile(cfg, uploadUrl, uploadToken, htmlName,
                    Encoding.UTF8.GetBytes(landingHtml), "text/html; charset=utf-8", ok => htmlUploaded = ok);

                string qrTarget = htmlUploaded ? htmlName : pngName;
                string qrUrl = $"{_downloadUrl}/file/{_bucketName}/{qrTarget}{tokenQuery}";
                LogManager.Info($"[B2] 업로드 완료 ({(htmlUploaded ? "랜딩 페이지" : "PNG 직링크 폴백")}, 링크 유효 {cfg.downloadAuthSeconds}초): {qrUrl}");
                onDone?.Invoke(qrUrl);
                yield break;
            }
        }

        // 랜딩 페이지 템플릿 로드. 파일이 없거나 깨져도 내장 최소 템플릿으로 동작한다 (예외로 죽지 않기)
        private static string LoadLandingTemplate(B2Config cfg)
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, cfg.landingTemplatePath);
                if (File.Exists(path)) return File.ReadAllText(path);
                LogManager.Warn($"[B2] 랜딩 템플릿 없음 — 내장 템플릿 사용: {path}");
            }
            catch (Exception e)
            {
                LogManager.Warn($"[B2] 랜딩 템플릿 로드 실패 — 내장 템플릿 사용: {e.Message}");
            }
            return "<!DOCTYPE html><html lang=\"ko\"><head><meta charset=\"utf-8\">" +
                   "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"></head>" +
                   "<body style=\"margin:0;background:#12141f;text-align:center;font-family:sans-serif\">" +
                   "<img src=\"{{IMAGE_URL}}\" style=\"width:100%\" alt=\"\">" +
                   "<p><a href=\"{{IMAGE_URL}}\" download=\"my-ai-car.png\" " +
                   "style=\"color:#f2b13f;font-size:1.2rem\">이미지 저장하기</a></p></body></html>";
        }

        // 비공개 버킷 파일의 만료형 다운로드 토큰 발급. 키에 shareFiles 권한이 필요하다
        // (버킷 제한 키를 UI에서 만들면 기본 포함 — 인수인계 §7)
        private IEnumerator GetDownloadAuth(B2Config cfg, string objectName, Action<string> onDone)
        {
            var body = new JObject
            {
                ["bucketId"] = _bucketId,
                ["fileNamePrefix"] = objectName,
                ["validDurationInSeconds"] = cfg.downloadAuthSeconds
            };
            using (var req = new UnityWebRequest(_apiUrl + "/b2api/v2/b2_get_download_authorization", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None)));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", _authToken);
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = (int)cfg.uploadTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    LogManager.Warn($"[B2] 다운로드 토큰 발급 실패 (HTTP {req.responseCode}): {req.error} / {req.downloadHandler.text}");
                    onDone(null);
                    yield break;
                }

                try
                {
                    onDone((string)JObject.Parse(req.downloadHandler.text)["authorizationToken"]);
                }
                catch (Exception e)
                {
                    LogManager.Warn($"[B2] 다운로드 토큰 응답 해석 실패: {e.Message}");
                    onDone(null);
                }
            }
        }

        private IEnumerator GetUploadUrl(B2Config cfg, Action<string, string, bool> onDone)
        {
            byte[] body = Encoding.UTF8.GetBytes("{\"bucketId\":\"" + _bucketId + "\"}");
            using (var req = new UnityWebRequest(_apiUrl + "/b2api/v2/b2_get_upload_url", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", _authToken);
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = (int)cfg.uploadTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    bool expired = req.responseCode == 401; // 토큰 만료 — 호출부가 재인증 후 재시도
                    LogManager.Warn($"[B2] 업로드 URL 발급 실패 (HTTP {req.responseCode}): {req.error} / {req.downloadHandler.text}");
                    onDone(null, null, expired);
                    yield break;
                }

                try
                {
                    JObject res = JObject.Parse(req.downloadHandler.text);
                    onDone((string)res["uploadUrl"], (string)res["authorizationToken"], false);
                }
                catch (Exception e)
                {
                    LogManager.Warn($"[B2] 업로드 URL 응답 해석 실패: {e.Message}");
                    onDone(null, null, false);
                }
            }
        }

        private IEnumerator PutFile(B2Config cfg, string uploadUrl, string uploadToken,
            string objectName, byte[] content, string contentType, Action<bool> onDone)
        {
            string sha1;
            using (var sha = SHA1.Create()) // B2가 무결성 검증용으로 요구하는 필수 헤더
            {
                var hex = new StringBuilder();
                foreach (byte b in sha.ComputeHash(content)) hex.Append(b.ToString("x2"));
                sha1 = hex.ToString();
            }

            using (var req = new UnityWebRequest(uploadUrl, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(content);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", uploadToken);
                req.SetRequestHeader("Content-Type", contentType);
                req.SetRequestHeader("X-Bz-File-Name", UnityWebRequest.EscapeURL(objectName));
                req.SetRequestHeader("X-Bz-Content-Sha1", sha1);
                req.timeout = (int)cfg.uploadTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    LogManager.Warn($"[B2] 업로드 실패 (HTTP {req.responseCode}): {req.error} / {req.downloadHandler.text}");
                    onDone(false);
                    yield break;
                }
            }
            onDone(true);
        }
    }
}
