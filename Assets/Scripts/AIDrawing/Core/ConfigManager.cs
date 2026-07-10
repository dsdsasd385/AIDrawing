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
        /// <summary>생성 전체 타임아웃(초). 계획서 12장: 초과 시 해당 세션만 사과 후 초기화.
        /// 콜드 스타트 첫 생성이 ~32초라 여유를 둔다(인수인계 §6). 워밍업이 있으면 실제 생성은 7~13초</summary>
        public float generateTimeoutSeconds = 45f;
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

    /// <summary>GCS 업로드 설정 (계획서 9-2). 버킷명·키가 비어 있으면 업로드/QR이 자동 비활성화된다</summary>
    [Serializable]
    public class GcsConfig
    {
        /// <summary>공개 버킷 이름. 비어 있으면 QR 기능 전체가 꺼진다</summary>
        public string bucketName = "";
        /// <summary>서비스 계정 키 JSON 경로. 절대 경로 또는 exe 옆(에디터: 프로젝트 루트) 기준 상대 경로.
        /// StreamingAssets 밖에 두는 이유: 키를 저장소·빌드 산출물에 싣지 않기 위함 (인수인계 §7)</summary>
        public string keyFilePath = "Config/gcs-key.json";
        /// <summary>버킷 안 객체 이름 접두사 (폴더 역할)</summary>
        public string objectPrefix = "cars/";
        /// <summary>업로드 요청 타임아웃(초). 초과 시 QR만 숨기고 체험은 계속</summary>
        public float uploadTimeoutSeconds = 15f;
    }

    /// <summary>Backblaze B2 업로드 설정 (계획서 9-2, 2026-07-10 GCS에서 전환 — 카드 없는 영구 무료 티어).
    /// 키 파일이 없으면 업로드/QR이 자동 비활성화된다. 버킷 정보는 버킷 제한 키(권장)의 authorize 응답이 제공</summary>
    [Serializable]
    public class B2Config
    {
        /// <summary>B2 키 JSON 경로 (keyId/applicationKey, 선택: bucketId/bucketName).
        /// 절대 경로 또는 exe 옆(에디터: 프로젝트 루트) 기준 상대 경로 — 저장소·빌드에 싣지 않는다 (인수인계 §7)</summary>
        public string keyFilePath = "Config/b2-key.json";
        /// <summary>버킷 안 객체 이름 접두사 (폴더 역할)</summary>
        public string objectPrefix = "cars/";
        /// <summary>QR 다운로드 링크 유효 시간(초). B2 공개 버킷은 카드 등록이 필요해서 비공개 버킷 +
        /// 만료형 다운로드 토큰으로 운영한다 (기본 604800 = 최대치 7일). 0이면 공개 버킷 가정(토큰 생략)</summary>
        public int downloadAuthSeconds = 604800;
        /// <summary>QR 랜딩 페이지(저장 버튼 포함) 템플릿의 StreamingAssets 기준 상대 경로.
        /// 파일이 없으면 내장 최소 템플릿으로 폴백한다 (예외로 죽지 않기)</summary>
        public string landingTemplatePath = "Data/QrLanding.html";
        /// <summary>요청 타임아웃(초). 초과 시 QR만 숨기고 체험은 계속</summary>
        public float uploadTimeoutSeconds = 15f;
    }

    /// <summary>VLM 필터 설정 (계획서 10장). 로컬 VLM 서버(OpenAI 호환 API)를 호출한다</summary>
    [Serializable]
    public class FilterConfig
    {
        /// <summary>필터 사용 여부. 꺼져 있으면 opt-in 작품이 곧장 갤러리로 간다</summary>
        public bool enabled = false;
        /// <summary>OpenAI 호환 chat completions 엔드포인트 (예: Ollama, llama.cpp 서버)</summary>
        public string endpoint = "http://127.0.0.1:11434/v1/chat/completions";
        /// <summary>사용할 VLM 모델 이름 (예: moondream, qwen2-vl)</summary>
        public string model = "moondream";
        /// <summary>판정 요청 타임아웃(초). 초과·실패 시 보수적으로 격리(계획서 10장 판정 정책)</summary>
        public float timeoutSeconds = 20f;
        /// <summary>VLM에 보내는 판정 질문. 영어인 이유: 경량 VLM은 영어 지시 추종이 훨씬 안정적이다.
        /// {"ok": true/false} 한 줄만 답하게 강제한다 (ContentFilter가 이 형식을 해석)</summary>
        public string question =
            "You are the content gate for a public exhibition gallery. " +
            "Image 1 is a visitor's hand-drawn sketch and image 2 is the AI-generated result. " +
            "Reply with only a JSON object: {\"ok\": true} if both images depict a car or vehicle doodle " +
            "and contain no sexual, violent, hateful, or otherwise inappropriate content or writing; " +
            "otherwise {\"ok\": false}.";
    }

    /// <summary>갤러리 슬라이드쇼 설정 (계획서 5장 Display 2)</summary>
    [Serializable]
    public class GalleryConfig
    {
        /// <summary>Display 2 슬라이드 전환 간격(초)</summary>
        public float slideIntervalSeconds = 8f;
        /// <summary>Gallery 폴더 재검색 간격(초). 새 작품 반영 주기</summary>
        public float rescanIntervalSeconds = 5f;
        /// <summary>대기 화면 미니 슬라이드쇼 전환 간격(초)</summary>
        public float attractSlideIntervalSeconds = 5f;
    }

    /// <summary>앱 전체 설정 묶음. StreamingAssets/Data/Config.json과 1:1 대응</summary>
    [Serializable]
    public class AppConfig
    {
        public ComfyUiConfig comfyUi = new ComfyUiConfig();
        public TimingConfig timing = new TimingConfig();
        public B2Config b2 = new B2Config();
        public GcsConfig gcs = new GcsConfig();
        public FilterConfig filter = new FilterConfig();
        public GalleryConfig gallery = new GalleryConfig();
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
