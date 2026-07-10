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
    /// Google Cloud Storage 업로더 — IResultUploader의 GCS 구현 (계획서 9-2).
    /// 결과 처리 시스템에 속하며 AppFlowManager가 생성 성공 직후 호출한다.
    /// 서비스 계정 키(JWT RS256 서명 → OAuth2 토큰 교환)로 인증하고 공개 버킷에 PNG를 올린다.
    /// 버킷명·키 파일이 없으면 IsConfigured=false — QR 기능 전체가 조용히 꺼진다 (계획서 3장: 부가 기능).
    /// </summary>
    public class GcsUploader : MonoBehaviour, IResultUploader
    {
        // 업로드에 필요한 최소 스코프. 키가 유출돼도 버킷 쓰기 이상은 못 하도록 좁게 잡는다 (인수인계 §7 최소 권한)
        private const string Scope = "https://www.googleapis.com/auth/devstorage.read_write";

        // 서비스 계정 키에서 읽은 자격 증명 (1회 로드 후 재사용)
        private string _clientEmail;
        private string _tokenUri;
        private RSA _rsa;
        private bool _credentialsLoaded;
        private bool _credentialsFailed;

        // 액세스 토큰 캐시. 매 업로드마다 토큰을 새로 받지 않도록 만료 전까지 재사용한다
        private string _accessToken;
        private float _tokenExpiresAt;

        /// <summary>버킷명과 서비스 계정 키가 갖춰져 업로드가 가능한지</summary>
        public bool IsConfigured
        {
            get
            {
                if (string.IsNullOrEmpty(ConfigManager.Config.gcs.bucketName)) return false;
                LoadCredentials();
                return !_credentialsFailed;
            }
        }

        /// <summary>
        /// 결과 PNG를 GCS 공개 버킷에 비동기 업로드한다. 실패 시 콜백에 null — 호출부는 QR만 숨긴다.
        /// </summary>
        /// <param name="sessionId">세션 ID (객체 이름에 포함)</param>
        /// <param name="png">결과 PNG 바이트</param>
        /// <param name="onDone">공개 URL 콜백 (실패 시 null)</param>
        public void Upload(string sessionId, byte[] png, Action<string> onDone)
        {
            if (!IsConfigured)
            {
                onDone?.Invoke(null);
                return;
            }
            StartCoroutine(UploadRoutine(sessionId, png, onDone));
        }

        private void OnDestroy()
        {
            _rsa?.Dispose();
        }

        // ── 자격 증명 로드 ──────────────────────────────────

        private void LoadCredentials()
        {
            if (_credentialsLoaded) return;
            _credentialsLoaded = true;
            _credentialsFailed = true; // 성공 시에만 해제

            try
            {
                string path = ResolveKeyPath(ConfigManager.Config.gcs.keyFilePath);
                if (path == null || !File.Exists(path))
                {
                    // 키 미배치는 정상 운영 상태일 수 있다(오프라인 전시) — 경고 1회만 남기고 QR을 끈다
                    LogManager.Warn($"[GCS] 서비스 계정 키 없음 — QR 기능 비활성: {path}");
                    return;
                }

                JObject key = JObject.Parse(File.ReadAllText(path));
                _clientEmail = (string)key["client_email"];
                _tokenUri = (string)key["token_uri"] ?? "https://oauth2.googleapis.com/token";
                string pem = (string)key["private_key"];
                if (string.IsNullOrEmpty(_clientEmail) || string.IsNullOrEmpty(pem))
                {
                    LogManager.Warn("[GCS] 키 파일에 client_email/private_key 없음 — QR 기능 비활성");
                    return;
                }

                // PEM(PKCS#8) 본문만 추출해 RSA 키로 가져온다
                string body = pem.Replace("-----BEGIN PRIVATE KEY-----", "")
                                 .Replace("-----END PRIVATE KEY-----", "")
                                 .Replace("\\n", "").Replace("\n", "").Replace("\r", "").Trim();
                _rsa = RSA.Create();
                _rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(body), out _);

                _credentialsFailed = false;
                LogManager.Info($"[GCS] 자격 증명 로드 완료: {_clientEmail}");
            }
            catch (Exception e)
            {
                LogManager.Warn($"[GCS] 자격 증명 로드 실패 — QR 기능 비활성: {e.Message}");
            }
        }

        // 키 파일 경로 해석. 상대 경로는 exe 옆(에디터에서는 프로젝트 루트) 기준 —
        // 키를 저장소·StreamingAssets에 두지 않기 위한 구조 (인수인계 §7: 커밋 금지)
        private static string ResolveKeyPath(string configured)
        {
            if (string.IsNullOrEmpty(configured)) return null;
            if (Path.IsPathRooted(configured)) return configured;
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, configured);
        }

        // ── OAuth2 토큰 발급 (JWT Bearer 흐름) ─────────────

        private IEnumerator EnsureToken(Action<string> onDone)
        {
            // 만료 60초 전까지는 캐시 재사용 (경계에서 만료된 토큰으로 업로드하는 것을 방지)
            if (_accessToken != null && Time.realtimeSinceStartup < _tokenExpiresAt - 60f)
            {
                onDone(_accessToken);
                yield break;
            }

            string jwt = null;
            try { jwt = BuildJwt(); }
            catch (Exception e) { LogManager.Warn($"[GCS] JWT 서명 실패: {e.Message}"); }
            if (jwt == null)
            {
                onDone(null);
                yield break;
            }

            var form = new WWWForm();
            form.AddField("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer");
            form.AddField("assertion", jwt);

            using (UnityWebRequest req = UnityWebRequest.Post(_tokenUri, form))
            {
                req.timeout = (int)ConfigManager.Config.gcs.uploadTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    LogManager.Warn($"[GCS] 토큰 발급 실패: {req.error} / {req.downloadHandler.text}");
                    onDone(null);
                    yield break;
                }

                try
                {
                    JObject res = JObject.Parse(req.downloadHandler.text);
                    _accessToken = (string)res["access_token"];
                    _tokenExpiresAt = Time.realtimeSinceStartup + ((float?)res["expires_in"] ?? 3600f);
                }
                catch (Exception e)
                {
                    LogManager.Warn($"[GCS] 토큰 응답 해석 실패: {e.Message}");
                    _accessToken = null;
                }
                onDone(_accessToken);
            }
        }

        // 서비스 계정 JWT 생성 + RS256 서명 (RFC 7523 — 구글 OAuth2가 요구하는 형식)
        private string BuildJwt()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
            var claims = new JObject
            {
                ["iss"] = _clientEmail,
                ["scope"] = Scope,
                ["aud"] = _tokenUri,
                ["iat"] = now,
                ["exp"] = now + 3600
            };
            string body = Base64Url(Encoding.UTF8.GetBytes(claims.ToString(Newtonsoft.Json.Formatting.None)));
            byte[] signature = _rsa.SignData(Encoding.ASCII.GetBytes(header + "." + body),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return header + "." + body + "." + Base64Url(signature);
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        // ── 업로드 ─────────────────────────────────────────

        private IEnumerator UploadRoutine(string sessionId, byte[] png, Action<string> onDone)
        {
            string token = null;
            yield return EnsureToken(t => token = t);
            if (token == null)
            {
                onDone?.Invoke(null);
                yield break;
            }

            GcsConfig cfg = ConfigManager.Config.gcs;
            // 세션 ID는 시각 기반이라 추측 가능 — 공개 버킷에서 타인 작품 URL을 유추하지 못하도록 난수 접미사를 붙인다
            string objectName = cfg.objectPrefix + sessionId + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".png";
            string uploadUrl = "https://storage.googleapis.com/upload/storage/v1/b/" + cfg.bucketName +
                               "/o?uploadType=media&name=" + UnityWebRequest.EscapeURL(objectName);

            using (var req = new UnityWebRequest(uploadUrl, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(png);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Authorization", "Bearer " + token);
                req.SetRequestHeader("Content-Type", "image/png");
                req.timeout = (int)cfg.uploadTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    LogManager.Warn($"[GCS] 업로드 실패: {req.error} / {req.downloadHandler.text}");
                    onDone?.Invoke(null);
                    yield break;
                }
            }

            string publicUrl = "https://storage.googleapis.com/" + cfg.bucketName + "/" + objectName;
            LogManager.Info($"[GCS] 업로드 완료: {publicUrl}");
            onDone?.Invoke(publicUrl);
        }
    }
}
