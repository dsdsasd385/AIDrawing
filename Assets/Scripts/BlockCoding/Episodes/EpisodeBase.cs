using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EarthCoding.Blocks;
using EarthCoding.Core;
using EarthCoding.Data;
using EarthCoding.UI;

namespace EarthCoding.Episodes
{
    /// <summary>
    /// 모든 에피소드 컨트롤러의 공통 부모. (작업계획서 6~7장 대응)
    /// 에피소드 시작 시 팔레트/결과 화면 구성, 실행 시 검증 → 순차 실행 → 점수 계산 →
    /// 결과 표시로 이어지는 공통 흐름을 제공한다.
    /// 각 에피소드(Episode1~5Controller)는 이 클래스를 상속해
    /// 결과 화면 구성(BuildResultArea)과 실행 로직(RunProgram)만 구현한다.
    /// EpisodeManager 가 생성하고 전환을 관리한다.
    /// </summary>
    public abstract class EpisodeBase : MonoBehaviour
    {
        /// <summary>이 에피소드의 JSON 데이터 (제목/블록/점수 기준)</summary>
        protected EpisodeData Data { get; private set; }

        /// <summary>공통 UI 접근용 축약 참조</summary>
        protected UIManager UI => UIManager.Instance;

        /// <summary>이번 실행에서 획득한 점수 (Finish 에서 기록)</summary>
        public int LastScore { get; private set; }

        /// <summary>이번 실행의 성공 여부 (Finish 에서 기록)</summary>
        public bool LastSuccess { get; private set; }

        /// <summary>
        /// 에피소드를 시작한다. 공통 UI 를 세팅하고 팔레트/결과 화면을 구성한다.
        /// </summary>
        /// <param name="data">에피소드 JSON 데이터</param>
        public void Begin(EpisodeData data)
        {
            Data = data;
            UI.SetupEpisodeUI(data);

            // 새 블록이 조립될 때 에피소드별 추가 UI(드롭다운 등)를 붙일 수 있게 구독
            UI.Assembly.OnBlockCreated += DecorateBlock;

            BuildPalette();
            BuildResultArea(UI.ResultArea);
            OnBegin();
            LogManager.Write("Info", $"Episode{data.EpisodeId} 시작: {data.EpisodeName}");
        }

        /// <summary>
        /// 에피소드를 종료한다. 이벤트 구독을 해제해 다음 에피소드와 겹치지 않게 한다.
        /// </summary>
        public void End()
        {
            if (UI != null && UI.Assembly != null)
                UI.Assembly.OnBlockCreated -= DecorateBlock;
            OnEnd();
        }

        /// <summary>
        /// 왼쪽 팔레트를 구성한다. 기본 동작은 JSON 의 모든 블록을 견본으로 추가.
        /// 팔레트 구성이 특수한 에피소드는 재정의한다.
        /// </summary>
        protected virtual void BuildPalette()
        {
            foreach (var entry in Data.Blocks)
                UI.AddPaletteBlock(entry);
        }

        /// <summary>
        /// 조립 영역에 새 블록이 놓일 때 호출된다.
        /// 드롭다운(늘리기/줄이기, 반복 횟수 등)이 필요한 에피소드가 재정의한다.
        /// </summary>
        /// <param name="view">방금 만들어진 블록 화면</param>
        protected virtual void DecorateBlock(BlockView view) { }

        /// <summary>
        /// 오른쪽 실시간 결과 영역을 구성한다. (지구/빙하/로봇 등 에피소드별 화면)
        /// </summary>
        /// <param name="area">결과 영역 루트</param>
        protected abstract void BuildResultArea(RectTransform area);

        /// <summary>에피소드 시작 직후 추가 초기화가 필요하면 재정의한다.</summary>
        protected virtual void OnBegin() { }

        /// <summary>에피소드 종료 시 정리가 필요하면 재정의한다.</summary>
        protected virtual void OnEnd() { }

        /// <summary>
        /// 초기화 버튼을 눌렀을 때 결과 화면을 시작 상태로 되돌린다.
        /// (블록 제거는 공통 처리이므로 여기서는 결과 화면만 담당)
        /// </summary>
        public virtual void ResetResultView() { }

        /// <summary>
        /// 실행 전 블록 검사. (실행 시스템의 '블록 검사 → 오류 확인' 단계)
        /// </summary>
        /// <param name="program">체험자가 조립한 블록 목록</param>
        /// <returns>오류 안내 문구 (문제 없으면 null)</returns>
        public virtual string ValidateProgram(List<BlockInstance> program)
        {
            // 공통 규칙: 명령 블록이 하나도 없으면 실행할 수 없다
            if (program.Count == 0)
                return "블록을 한 개 이상 조립한 뒤 실행해 주세요!";
            return null;
        }

        /// <summary>
        /// 프로그램을 순차 실행하는 코루틴. 각 에피소드가 구현한다.
        /// 실행이 끝나면 반드시 Finish(점수, 성공여부) 를 호출해야 한다.
        /// </summary>
        /// <param name="program">실행할 블록 목록 (조립 순서)</param>
        public abstract IEnumerator RunProgram(List<BlockInstance> program);

        /// <summary>
        /// 실행 완료 처리: 점수 저장 + 점수/상태 표시 + 결과 팝업. (실행 시스템의 마지막 단계)
        /// </summary>
        /// <param name="score">획득 점수</param>
        /// <param name="success">성공 여부</param>
        protected void Finish(int score, bool success)
        {
            LastScore = score;
            LastSuccess = success;

            ScoreManager.SetScore(Data.EpisodeId, score);
            UI.ScoreText.text = $"점수: {score}점";
            UI.StatusText.text = success ? "성공!" : "실패";
            UI.StatusText.color = success ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.6f, 0.5f);

            // 결과 문구는 JSON 에서 관리 (운영자 수정 가능)
            UI.ShowPopup(success ? "성공!" : "아쉬워요",
                (success ? Data.SuccessText : Data.FailText) + $"\n\n획득 점수: {score}점");

            // 효과음 이름도 데이터 주도: 리소스가 생기면 자동 재생된다
            AudioManager.Instance.PlaySfx(success ? "Success" : "Fail");
            LogManager.Write("Info", $"Episode{Data.EpisodeId} 실행 결과: {(success ? "성공" : "실패")} / {score}점");
        }

        /// <summary>
        /// 실행 중 상태 문구를 갱신하는 도우미. (예: "화력 발전 실행 중...")
        /// </summary>
        /// <param name="message">표시할 문구</param>
        protected void SetStatus(string message)
        {
            UI.StatusText.text = message;
            UI.StatusText.color = UIFactory.TextColor;
        }

        /// <summary>
        /// 블록 실행 사이의 대기 시간. Config 에서 애니메이션을 끄면 대기 없이 즉시 진행된다.
        /// </summary>
        protected WaitForSeconds StepWait =>
            DataManager.Config.UseAnimation ? new WaitForSeconds(0.7f) : null;
    }
}
