using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using EarthCoding.Blocks;
using EarthCoding.UI;

namespace EarthCoding.Episodes
{
    /// <summary>
    /// Episode 5 「지구 회복하기」 컨트롤러. 학습 요소: 함수. (작업계획서 6장)
    /// 체험자는 '지구 회복 프로젝트' 함수를 단계 블록(탄소 줄이기→신재생에너지→
    /// 쓰레기 줄이기→계산하기)으로 정의하고, 함수 호출 블록으로 실행한다.
    /// 호출 블록 하나가 함수 전체(여러 단계)를 실행하는 것을 보여주어
    /// '한 번 만든 함수를 여러 번 재사용한다'는 개념을 시각화한다.
    /// 점수: 호출마다 (정순서 단계 수 × StepHeal) 만큼 지구가 회복되며
    /// 총 회복량이 곧 점수가 된다 (1~50점, Episode5.json Params 로 관리).
    /// </summary>
    public class Episode5Controller : EpisodeBase
    {
        // ---------- 오른쪽 결과 화면 요소 ----------

        /// <summary>지구 이미지 (회복될수록 회색 → 초록/파랑으로 변한다)</summary>
        private Image _earth;

        /// <summary>회복 게이지 막대</summary>
        private Image _healBar;

        /// <summary>회복 수치 텍스트</summary>
        private Text _healText;

        /// <summary>함수 실행 과정 표시 텍스트 (현재 실행 중인 단계)</summary>
        private Text _funcText;

        /// <summary>현재 회복량 (점수와 동일한 값)</summary>
        private float _healing;

        /// <summary>점수 상한 (게이지 환산 기준, JSON MaxScore)</summary>
        private float _maxScore;

        /// <summary>
        /// 오른쪽 결과 화면 구성: 병든 지구 + 회복 게이지 + 함수 실행 표시.
        /// </summary>
        /// <param name="area">결과 영역 루트</param>
        protected override void BuildResultArea(RectTransform area)
        {
            _maxScore = Data.GetParamFloat("MaxScore", 50f);

            // 함수 실행 과정 (상단)
            _funcText = UIFactory.Label(area, "FuncText", "", 19);
            var funcRt = (RectTransform)_funcText.transform;
            funcRt.anchorMin = new Vector2(0.05f, 0.84f);
            funcRt.anchorMax = new Vector2(0.95f, 0.97f);
            _funcText.color = new Color(1f, 0.85f, 0.6f);

            // 지구 (가운데)
            _earth = UIFactory.Panel(area, "Earth",
                new Vector2(0.32f, 0.4f), new Vector2(0.68f, 0.8f), Color.gray);
            _earth.raycastTarget = false;
            UIFactory.Label(_earth.transform, "EarthLabel", "지구", 22).raycastTarget = false;

            // 회복 게이지
            var barBg = UIFactory.Panel(area, "HealBarBg",
                new Vector2(0.1f, 0.24f), new Vector2(0.9f, 0.34f), new Color(0, 0, 0, 0.35f));
            barBg.raycastTarget = false;
            _healBar = UIFactory.Panel(barBg.transform, "HealBar",
                new Vector2(0, 0), new Vector2(0, 1), new Color(0.3f, 0.8f, 0.5f));
            _healBar.raycastTarget = false;

            // 회복 수치
            _healText = UIFactory.Label(area, "HealText", "", 20);
            var healRt = (RectTransform)_healText.transform;
            healRt.anchorMin = new Vector2(0.05f, 0.05f);
            healRt.anchorMax = new Vector2(0.95f, 0.2f);

            ResetResultView();
        }

        /// <summary>결과 화면을 병든 지구 상태로 되돌린다.</summary>
        public override void ResetResultView()
        {
            _healing = 0f;
            _funcText.text = "함수를 완성하고 호출해 보세요!";
            ApplyHealVisual();
        }

        /// <summary>
        /// 실행 전 검사: 함수 개념 학습을 위해
        /// 단계 블록(함수 정의)과 호출 블록이 모두 필요하다.
        /// </summary>
        /// <param name="program">조립된 블록 목록</param>
        public override string ValidateProgram(List<BlockInstance> program)
        {
            var baseError = base.ValidateProgram(program);
            if (baseError != null) return baseError;

            bool hasStep = program.Any(b => b.Entry.Type == "Function");
            bool hasCall = program.Any(b => b.Entry.Extra == "call");

            if (!hasStep)
                return "함수 안에 들어갈 단계 블록을 넣어 주세요!\n(탄소 줄이기, 신재생에너지, 쓰레기 줄이기, 계산하기)";
            if (!hasCall)
                return "'지구 회복 프로젝트 호출' 블록이 있어야\n함수가 실행돼요!";
            return null;
        }

        /// <summary>
        /// 프로그램 실행: 조립된 단계 블록들이 함수 본문이 되고,
        /// 호출 블록을 만날 때마다 함수 전체가 실행된다.
        /// </summary>
        /// <param name="program">조립된 블록 목록</param>
        public override IEnumerator RunProgram(List<BlockInstance> program)
        {
            ResetResultView();

            // ----- 함수 정의 해석: 단계 블록들의 조립 순서가 함수 본문이 된다 -----
            var steps = program.Where(b => b.Entry.Type == "Function").ToList();

            // 정답 순서(Extra 1~4)와 비교해 정순서 단계 수를 계산한다
            var answer = Data.Blocks
                .Where(b => b.Type == "Function")
                .OrderBy(b => int.TryParse(b.Extra, out var o) ? o : 99)
                .Select(b => b.Id)
                .ToList();
            int correctSteps = 0;
            for (int i = 0; i < steps.Count && i < answer.Count; i++)
                if (steps[i].Entry.Id == answer[i]) correctSteps++;

            // 호출 1회당 회복량 = 정순서 단계 수 × StepHeal (JSON 관리)
            float healPerCall = correctSteps * Data.GetParamFloat("StepHeal", 2f);
            int maxCalls = Data.GetParamInt("MaxCalls", 6);
            int callCount = 0;

            // ----- 프로그램 순차 실행: 호출 블록을 만나면 함수 본문을 실행한다 -----
            foreach (var block in program)
            {
                if (block.Entry.Extra != "call") continue;   // 단계 블록 자체는 정의일 뿐 실행되지 않는다

                callCount++;
                if (callCount > maxCalls)
                {
                    // 호출 횟수 상한 초과분은 실행하지 않는다 (점수 상한 관리)
                    _funcText.text = $"호출은 최대 {maxCalls}번까지만 실행돼요!";
                    yield return StepWait;
                    break;
                }

                // 함수 호출 연출: 호출 블록 하나가 함수 전체 단계를 실행함을 보여준다
                SetStatus($"{callCount}번째 함수 호출!");
                _funcText.text = $"[호출 {callCount}] 지구 회복 프로젝트 실행!";
                yield return StepWait;

                foreach (var step in steps)
                {
                    _funcText.text = $"[호출 {callCount}] {step.Entry.Name} 실행 중...";
                    yield return new WaitForSeconds(Core.DataManager.Config.UseAnimation ? 0.35f : 0f);
                }

                // 함수 실행 결과 반영: 지구가 회복된다
                _healing = Mathf.Min(_maxScore, _healing + healPerCall);
                ApplyHealVisual();
                yield return StepWait;
            }

            _funcText.text = correctSteps >= answer.Count
                ? "지구 회복 프로젝트 완성! 지구가 살아났어요!"
                : $"함수 단계가 {correctSteps}/{answer.Count}개만 맞았어요.";

            // ----- 점수 계산: 총 회복량 = 점수 (1~50점, JSON Params) -----
            int minScore = Data.GetParamInt("MinScore", 1);
            int score = Mathf.Clamp(Mathf.RoundToInt(_healing), minScore, (int)_maxScore);

            // 함수 4단계를 모두 정순서로 완성했을 때만 성공으로 판정한다
            Finish(score, correctSteps >= answer.Count);
        }

        /// <summary>
        /// 회복량을 화면에 반영한다: 지구 색(회색→초록), 게이지, 수치.
        /// </summary>
        private void ApplyHealVisual()
        {
            float ratio = _maxScore > 0 ? Mathf.Clamp01(_healing / _maxScore) : 0f;

            // 병든 회색 지구 → 건강한 초록 지구
            _earth.color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.3f, 0.8f, 0.55f), ratio);
            _healBar.rectTransform.anchorMax = new Vector2(ratio, 1);
            _healText.text = $"지구 회복: {_healing:0} / {_maxScore:0}";
        }
    }
}
