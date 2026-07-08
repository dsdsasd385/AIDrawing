using UnityEngine;
using UnityEngine.UI;
using EarthCoding.Core;

namespace EarthCoding.UI
{
    /// <summary>
    /// 화면 보호 모드. (작업계획서 21장 '전시장 운영 기능' 대응)
    /// 인트로(대기) 화면에서 추가로 일정 시간(설정의 IdleTimeoutSec) 동안
    /// 입력이 없으면 어두운 화면 보호 오버레이를 띄워 모니터 번인(burn-in)을 줄인다.
    /// 아무 곳이나 터치하면 즉시 해제되고 인트로 화면으로 돌아간다.
    /// GameManager 가 생성하고 인트로 상태를 알려준다.
    /// </summary>
    public class ScreenSaver : MonoBehaviour
    {
        /// <summary>루트 캔버스</summary>
        private Canvas _canvas;

        /// <summary>화면 보호 오버레이 루트</summary>
        private GameObject _overlay;

        /// <summary>둥실거리는 안내 문구</summary>
        private Text _message;

        /// <summary>마지막 입력 시각 (화면 보호 진입 판정)</summary>
        private float _lastInputTime;

        /// <summary>현재 인트로(대기) 화면인지 여부. GameManager 가 갱신한다.</summary>
        public bool AtIntro { get; set; }

        /// <summary>
        /// 초기화: 오버레이를 미리 만들어 두고 숨긴다.
        /// </summary>
        /// <param name="canvas">루트 캔버스</param>
        public void Initialize(Canvas canvas)
        {
            _canvas = canvas;

            // 화면 보호 오버레이 (거의 검은 화면 + 안내 문구)
            var dim = UIFactory.Panel(_canvas.transform, "ScreenSaver",
                Vector2.zero, Vector2.one, new Color(0.01f, 0.02f, 0.05f, 0.98f));
            _overlay = dim.gameObject;

            _message = UIFactory.Label(_overlay.transform, "Message",
                "지구 환경 코딩 체험\n\n화면을 터치해 주세요!", 40);
            _message.fontStyle = FontStyle.Bold;

            _overlay.SetActive(false);
            _lastInputTime = Time.unscaledTime;
        }

        /// <summary>
        /// 매 프레임: 입력 감지와 화면 보호 진입/해제 판정.
        /// </summary>
        private void Update()
        {
            if (_overlay == null) return;

            // 아무 입력이나 있으면: 화면 보호 해제 + 타이머 리셋
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0)
            {
                _lastInputTime = Time.unscaledTime;
                if (_overlay.activeSelf)
                {
                    _overlay.SetActive(false);
                    LogManager.Write("Info", "화면 보호 모드 해제");
                }
                return;
            }

            // 인트로 화면에서 추가로 IdleTimeout 만큼 더 방치되면 화면 보호 진입
            float timeout = DataManager.Config.IdleTimeoutSec;
            if (!_overlay.activeSelf && AtIntro && timeout > 0
                && Time.unscaledTime - _lastInputTime > timeout)
            {
                _overlay.SetActive(true);
                _overlay.transform.SetAsLastSibling();   // 모든 UI 위에 표시
                LogManager.Write("Info", "화면 보호 모드 진입");
            }

            // 화면 보호 중에는 문구가 천천히 깜빡여 살아있음을 알린다
            if (_overlay.activeSelf)
            {
                float alpha = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.5f);
                _message.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.25f, 1f, alpha));
            }
        }
    }
}
