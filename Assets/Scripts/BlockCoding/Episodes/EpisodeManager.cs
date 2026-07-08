using System;
using System.Collections;
using UnityEngine;
using EarthCoding.Core;
using EarthCoding.UI;

namespace EarthCoding.Episodes
{
    /// <summary>
    /// 에피소드 전환/실행 관리 매니저. (작업계획서 3장 '전체 체험 플로우', 7장 '실행 시스템' 대응)
    /// 에피소드 1~5 컨트롤러를 보관하고 순차 진행(다음 버튼), 스킵, 재진입을 처리한다.
    /// 하단 버튼(초기화/힌트/실행)의 실제 동작도 이 클래스가 담당한다.
    /// GameManager 가 생성하며, 마지막 에피소드가 끝나면 GameManager 에 통지한다.
    /// </summary>
    public class EpisodeManager : MonoBehaviour
    {
        /// <summary>전역 접근용 싱글턴</summary>
        public static EpisodeManager Instance { get; private set; }

        /// <summary>현재 진행 중인 에피소드 번호 (1~5, 시작 전 0)</summary>
        public int CurrentEpisodeId { get; private set; }

        /// <summary>실행 애니메이션 진행 중 여부 (중복 실행 방지)</summary>
        public bool IsRunning { get; private set; }

        /// <summary>마지막 에피소드까지 끝났을 때 GameManager 가 받을 통지</summary>
        public event Action OnAllEpisodesFinished;

        /// <summary>에피소드 번호 → 컨트롤러 (Awake 에서 생성)</summary>
        private EpisodeBase[] _episodes;

        /// <summary>현재 활성 에피소드 컨트롤러</summary>
        private EpisodeBase _current;

        /// <summary>
        /// 에피소드 컨트롤러들을 생성하고 UI 버튼 이벤트를 연결한다.
        /// GameManager 가 UI 생성 직후 호출한다.
        /// </summary>
        public void Initialize()
        {
            Instance = this;

            // 에피소드 컨트롤러를 같은 오브젝트의 컴포넌트로 추가한다.
            // 새 에피소드 추가 시 이 배열에만 등록하면 된다 (작업계획서 24장 확장성).
            _episodes = new EpisodeBase[]
            {
                gameObject.AddComponent<Episode1Controller>(),
                gameObject.AddComponent<Episode2Controller>(),
                gameObject.AddComponent<Episode3Controller>(),
                gameObject.AddComponent<Episode4Controller>(),
                gameObject.AddComponent<Episode5Controller>(),
            };

            // 하단 공통 버튼 동작 연결
            var ui = UIManager.Instance;
            ui.OnResetClicked += HandleReset;
            ui.OnHintClicked += HandleHint;
            ui.OnRunClicked += HandleRun;
            ui.OnNextClicked += HandleNext;
        }

        /// <summary>
        /// 지정한 에피소드를 시작한다. 관리자 모드의 '에피소드 선택'과 재진입에도 사용한다.
        /// </summary>
        /// <param name="episodeId">에피소드 번호 (1~5)</param>
        public void StartEpisode(int episodeId)
        {
            if (episodeId < 1 || episodeId > _episodes.Length)
            {
                LogManager.Write("Warning", $"잘못된 에피소드 번호: {episodeId}");
                return;
            }

            // 이전 에피소드 정리 후 새 에피소드 시작
            _current?.End();
            CurrentEpisodeId = episodeId;
            _current = _episodes[episodeId - 1];
            _current.Begin(DataManager.GetEpisode(episodeId));
        }

        /// <summary>초기화 버튼: 조립한 블록을 모두 지우고 결과 화면을 처음 상태로 되돌린다.</summary>
        private void HandleReset()
        {
            if (IsRunning || _current == null) return;
            UIManager.Instance.Assembly.Clear();
            _current.ResetResultView();
            UIManager.Instance.StatusText.text = "블록을 조립하고 실행을 눌러보세요!";
            UIManager.Instance.ScoreText.text = "점수: -";
        }

        /// <summary>힌트 버튼: JSON 의 힌트 문구를 팝업으로 보여준다. (정답이 아닌 방향 제시)</summary>
        private void HandleHint()
        {
            if (_current == null) return;
            var data = DataManager.GetEpisode(CurrentEpisodeId);
            UIManager.Instance.ShowPopup("힌트", data.Hint);
        }

        /// <summary>
        /// 실행 버튼: 블록 검사 → 오류 확인 → 순차 실행 → 점수 계산 → 결과 표시.
        /// (작업계획서 7장 실행 시스템 흐름)
        /// </summary>
        private void HandleRun()
        {
            if (IsRunning || _current == null) return;

            var program = UIManager.Instance.Assembly.GetProgram();

            // 1) 블록 검사: 문제가 있으면 오류 팝업을 띄우고 실행하지 않는다
            var error = _current.ValidateProgram(program);
            if (error != null)
            {
                UIManager.Instance.ShowPopup("잠깐!", error);
                return;
            }

            // 2) 순차 실행 (코루틴, 실행 중 버튼 잠금)
            StartCoroutine(RunRoutine());

            IEnumerator RunRoutine()
            {
                IsRunning = true;
                UIManager.Instance.SetButtonsInteractable(false);

                yield return StartCoroutine(_current.RunProgram(program));

                UIManager.Instance.SetButtonsInteractable(true);
                IsRunning = false;
            }
        }

        /// <summary>
        /// 다음 버튼: 다음 에피소드로 순차 진행한다. 실행하지 않아도 넘어갈 수 있다(스킵 가능).
        /// 마지막 에피소드에서는 클로징으로 넘어가도록 GameManager 에 통지한다.
        /// </summary>
        private void HandleNext()
        {
            if (IsRunning) return;

            if (CurrentEpisodeId >= _episodes.Length)
            {
                // 모든 에피소드 완료 → 클로징 진행
                _current?.End();
                OnAllEpisodesFinished?.Invoke();
            }
            else
            {
                StartEpisode(CurrentEpisodeId + 1);
            }
        }
    }
}
