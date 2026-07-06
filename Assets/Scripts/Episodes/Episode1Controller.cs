using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EarthCoding.Blocks;
using EarthCoding.UI;

namespace EarthCoding.Episodes
{
    /// <summary>
    /// Episode 1 「지구 온도 낮추기」 컨트롤러. 학습 요소: 변수. (작업계획서 6장)
    /// 체험자는 블록(화력 발전/전기 자동차/공원/미세먼지)마다
    /// '늘리기/줄이기' 변수를 선택해 CO₂ 수치를 조절한다.
    /// 실행하면 블록 순서대로 CO₂가 변하고, 지구 색상과 온도가 실시간으로 바뀐다.
    /// 최종 CO₂ 수치에 따라 0~3점을 획득한다. 점수 기준은 Episode1.json 의 Params 로 관리.
    /// </summary>
    public class Episode1Controller : EpisodeBase
    {
        // ---------- 오른쪽 결과 화면 요소 ----------

        /// <summary>지구 이미지 (CO₂에 따라 파랑↔빨강으로 색이 변한다)</summary>
        private Image _earth;

        /// <summary>CO₂ 게이지 막대 (수치에 비례해 길이 변화)</summary>
        private Image _co2Bar;

        /// <summary>CO₂ 수치 텍스트</summary>
        private Text _co2Text;

        /// <summary>지구 온도 텍스트</summary>
        private Text _tempText;

        /// <summary>현재 표시 중인 CO₂ 수치 (애니메이션 보간용)</summary>
        private float _displayCo2;

        /// <summary>변수 선택지: 0=늘리기, 1=줄이기 (드롭다운 순서와 일치해야 함)</summary>
        private static readonly string[] VariableOptions = { "늘리기", "줄이기" };

        /// <summary>
        /// 조립된 블록에 '늘리기/줄이기' 변수 드롭다운을 붙인다. (변수 학습 요소)
        /// </summary>
        /// <param name="view">방금 조립 영역에 놓인 블록</param>
        protected override void DecorateBlock(BlockView view)
        {
            var dd = UIFactory.DropdownBox(view.OptionArea, "VariableDropdown", VariableOptions, 18);
            // 선택값을 블록 인스턴스에 저장해 실행 시 사용한다
            dd.onValueChanged.AddListener(v => view.Instance.OptionA = v);
        }

        /// <summary>
        /// 오른쪽 결과 화면 구성: 지구 + CO₂ 게이지 + 온도 표시.
        /// </summary>
        /// <param name="area">결과 영역 루트</param>
        protected override void BuildResultArea(RectTransform area)
        {
            // 지구 (리소스 적용 시 일러스트로 교체될 자리)
            _earth = UIFactory.Panel(area, "Earth",
                new Vector2(0.3f, 0.45f), new Vector2(0.7f, 0.95f), Color.white);
            _earth.raycastTarget = false;
            UIFactory.Label(_earth.transform, "EarthLabel", "지구", 24).raycastTarget = false;

            // CO₂ 게이지 배경
            var barBg = UIFactory.Panel(area, "Co2BarBg",
                new Vector2(0.1f, 0.28f), new Vector2(0.9f, 0.38f), new Color(0, 0, 0, 0.35f));
            barBg.raycastTarget = false;

            // CO₂ 게이지 막대 (anchorMax.x 를 조절해 길이 표현)
            _co2Bar = UIFactory.Panel(barBg.transform, "Co2Bar",
                new Vector2(0, 0), new Vector2(0.5f, 1), new Color(0.9f, 0.55f, 0.2f));
            _co2Bar.raycastTarget = false;

            // CO₂ 수치 / 온도 텍스트
            _co2Text = UIFactory.Label(area, "Co2Text", "", 20);
            var co2Rt = (RectTransform)_co2Text.transform;
            co2Rt.anchorMin = new Vector2(0.1f, 0.15f);
            co2Rt.anchorMax = new Vector2(0.9f, 0.27f);

            _tempText = UIFactory.Label(area, "TempText", "", 20);
            var tempRt = (RectTransform)_tempText.transform;
            tempRt.anchorMin = new Vector2(0.1f, 0.02f);
            tempRt.anchorMax = new Vector2(0.9f, 0.14f);

            ResetResultView();
        }

        /// <summary>결과 화면을 시작 CO₂ 상태로 되돌린다. (초기화 버튼)</summary>
        public override void ResetResultView()
        {
            _displayCo2 = Data.GetParamFloat("StartCo2", 100f);
            ApplyCo2Visual(_displayCo2);
        }

        /// <summary>
        /// 프로그램 순차 실행: 블록마다 변수(늘리기/줄이기)에 따라 CO₂를 바꾸고
        /// 지구 색/게이지를 애니메이션한다. 완료 후 점수를 계산한다.
        /// </summary>
        /// <param name="program">조립된 블록 목록</param>
        public override IEnumerator RunProgram(List<BlockInstance> program)
        {
            float co2 = Data.GetParamFloat("StartCo2", 100f);
            _displayCo2 = co2;
            ApplyCo2Visual(co2);

            foreach (var block in program)
            {
                // 변수 적용: 늘리기(0)는 +Value, 줄이기(1)는 -Value
                bool increase = block.OptionA == 0;
                float delta = increase ? block.Entry.Value : -block.Entry.Value;
                co2 = Mathf.Max(0, co2 + delta);

                SetStatus($"{block.Entry.Name} {(increase ? "늘리기" : "줄이기")} 실행 중...");

                // CO₂ 수치가 부드럽게 변하는 애니메이션
                yield return AnimateCo2To(co2);
                yield return StepWait;
            }

            // ----- 점수 계산: 최종 CO₂가 낮을수록 높은 점수 (기준은 JSON Params) -----
            int score;
            if (co2 <= Data.GetParamFloat("Score3Co2", 45f)) score = 3;
            else if (co2 <= Data.GetParamFloat("Score2Co2", 70f)) score = 2;
            else if (co2 <= Data.GetParamFloat("Score1Co2", 99f)) score = 1;
            else score = 0;

            // 2점 이상을 '지구 온도 낮추기 성공' 으로 판정한다
            Finish(score, score >= 2);
        }

        /// <summary>
        /// CO₂ 표시값을 목표치까지 부드럽게 보간하는 애니메이션 코루틴.
        /// </summary>
        /// <param name="target">목표 CO₂ 수치</param>
        private IEnumerator AnimateCo2To(float target)
        {
            // 애니메이션이 꺼져 있으면 즉시 반영 (Config.UseAnimation)
            if (!Core.DataManager.Config.UseAnimation)
            {
                _displayCo2 = target;
                ApplyCo2Visual(target);
                yield break;
            }

            float start = _displayCo2;
            const float duration = 0.5f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _displayCo2 = Mathf.Lerp(start, target, t / duration);
                ApplyCo2Visual(_displayCo2);
                yield return null;
            }
            _displayCo2 = target;
            ApplyCo2Visual(target);
        }

        /// <summary>
        /// CO₂ 수치를 화면에 반영한다: 게이지 길이, 수치, 온도, 지구 색상.
        /// </summary>
        /// <param name="co2">표시할 CO₂ 수치</param>
        private void ApplyCo2Visual(float co2)
        {
            // 게이지: 0~150 범위를 0~100% 길이로 환산
            float ratio = Mathf.Clamp01(co2 / 150f);
            _co2Bar.rectTransform.anchorMax = new Vector2(ratio, 1);

            // 온도: CO₂에 비례하는 단순 환산식 (교육용 시각화 목적)
            float temp = 13f + co2 * 0.03f;

            _co2Text.text = $"CO₂ 수치: {co2:0}";
            _tempText.text = $"지구 온도: {temp:0.0}℃";

            // 지구 색: CO₂ 낮음=파랑(시원) ↔ 높음=빨강(뜨거움)
            _earth.color = Color.Lerp(new Color(0.25f, 0.6f, 0.95f), new Color(0.9f, 0.3f, 0.25f), ratio);
        }
    }
}
