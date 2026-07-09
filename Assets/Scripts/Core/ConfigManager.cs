using System;
using System.IO;
using UnityEngine;

namespace CarDrawing.Core
{
    /// <summary>ComfyUI 연동 설정 (계획서 7장)</summary>
    [Serializable]
    public class ComfyUiConfig
    {
        /// <summary>서버 주소</summary>
        public string baseUrl = "http://127.0.0.1:8188";
        /// <summary>StreamingAssets 기준 워크플로 JSON 상대 경로</summary>
        public string workflowPath = "ComfyUI/car_workflow_api.json";
        /// <summary>완료 폴링 간격(초). 계획서 7장: 0.5초</summary>
        public float pollIntervalSeconds = 0.5f;
        /// <summary>생성 전체 타임아웃(초). 계획서 12장: 초과 시 해당 세션만 사과 후 초기화</summary>
        public float generateTimeoutSeconds = 30f;
        /// <summary>제출 재시도 횟수. 업로드 직후 첫 제출 실패가 실측된 함정 (인수인계 §6)</summary>
        public int submitMaxRetries = 1;
    }

    /// <summary>시간 정책 (계획서 4장)</summary>
    [Serializable]
    public class TimingConfig
    {
        /// <summary>그리기 화면 무입력 → 팝업까지(초)</summary>
        public float idlePopupSeconds = 90f;
        /// <summary>팝업 후 무응답 → 대기 복귀까지 유예(초)</summary>
        public float idlePopupGraceSeconds = 30f;
        /// <summary>스타일 선택 화면 방치 → 대기 복귀(초). 계획서에 명시되지 않아 결과 화면과 동일 값 적용</summary>
        public float styleTimeoutSeconds = 60f;
        /// <summary>결과 화면 자동 복귀(초)</summary>
        public float resultReturnSeconds = 60f;
        /// <summary>생성 실패 사과 문구 표시 시간(초)</summary>
        public float errorNoticeSeconds = 4f;
    }

    /// <summary>GCS 업로드 설정 (④에서 사용, 계획서 9장)</summary>
    [Serializable]
    public class GcsConfig
    {
        public string bucketName = "";
        public string keyFilePath = "";
    }

    /// <summary>VLM 필터 설정 (④에서 사용, 계획서 10장)</summary>
    [Serializable]
    public class FilterConfig
    {
        public bool enabled = false;
    }

    /// <summary>앱 전체 설정 묶음. StreamingAssets/Data/Config.json과 1:1 대응</summary>
    [Serializable]
    public class AppConfig
    {
        public ComfyUiConfig comfyUi = new ComfyUiConfig();
        public TimingConfig timing = new TimingConfig();
        public GcsConfig gcs = new GcsConfig();
        public FilterConfig filter = new FilterConfig();
    }

    /// <summary>
    /// Config.json 로더. 코어 시스템에 속하며 모든 시스템이 설정을 여기서 읽는다.
    /// 파일이 없거나 깨져도 필드 초기값으로 계속 동작한다 (계획서 12장: 예외로 죽지 않기).
    /// </summary>
    public static class ConfigManager
    {
        private static AppConfig _config;

        /// <summary>현재 설정. 첫 접근 시 JSON을 읽고, 실패하면 기본값을 쓴다</summary>
        public static AppConfig Config => _config ?? (_config = Load());

        /// <summary>관리자 모드에서 JSON을 다시 읽을 때 사용한다 (계획서 11장).</summary>
        public static void Reload() => _config = null;

        private static AppConfig Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Data", "Config.json");
            try
            {
                if (File.Exists(path))
                {
                    // JsonUtility는 JSON에 없는 필드를 초기값 그대로 두므로 부분 설정 파일도 안전하다
                    var loaded = JsonUtility.FromJson<AppConfig>(File.ReadAllText(path));
                    if (loaded != null) return loaded;
                }
                LogManager.Warn($"[Config] 설정 파일 없음 — 기본값 사용: {path}");
            }
            catch (Exception e)
            {
                LogManager.Error($"[Config] 설정 로드 실패 — 기본값 사용: {e.Message}");
            }
            return new AppConfig();
        }
    }
}
