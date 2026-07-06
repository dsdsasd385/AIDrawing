using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EarthCoding.Blocks;
using EarthCoding.UI;

namespace EarthCoding.Episodes
{
    /// <summary>
    /// Episode 4 「탄소 배출 줄이기」 컨트롤러. 학습 요소: 반복문. (작업계획서 6장)
    /// 체험자는 행동 블록(좋은 행동 4종 / 나쁜 행동 4종)을 조립하고
    /// 블록마다 반복 횟수를 정한다. 실행하면 행동이 횟수만큼 반복 실행되며
    /// 탄소 발자국이 실시간으로 커지거나 작아진다.
    /// 행동별 1회 변화량(Value)·시작 수치·점수 환산은 Episode4.json 으로 관리한다.
    /// 점수: 탄소 감소량 ÷ ScorePerReduce (0점 이상, 상한 없음 - 작업계획서 '0점 이상').
    /// </summary>
    public class Episode4Controller : EpisodeBase
    {
        // ---------- 오른쪽 결과 화면 요소 ----------

        /// <summary>탄소 발자국 패널 (수치에 비례해 커지고 색이 짙어진다)</summary>
        private Image _footprint;

        /// <summary>발자국 안 수치 라벨</summary>
        private Text _footprintLabel;

        /// <summary>탄소 게이지 막대</summary>
        private Image _carbonBar;

        /// <summary>탄소 수치/변화 텍스트</summary>
        private Text _carbonText;

        /// <summary>반복 실행 상황 텍스트 (예: 걷기 3/5회)</summary>
        private Text _repeatText;

        /// <summary>현재 표시 중인 탄소 수치</summary>
        private float _displayCarbon;

        /// <summary>
        /// 반복 횟수 드롭다운을 블록에 붙인다. (반복문 학습 요소)
        /// OptionB = 선택 인덱스, 실제 반복 횟수 = OptionB + 1.
        /// </summary>
        /// <param name="view">방금 조립된 블록</param>
        protected override void DecorateBlock(BlockView view)
        {
            // 1회 ~ MaxRepeat회 선택지 구성 (기본 5회까지, JSON 으로 조정 가능)
            int max = Data.GetParamInt("MaxRepeat", 5);
            var options = new string[max];
            for (int i = 0; i < max; i++)
                options[i] = $"{i + 1}회 반복";

            var dd = UIFactory.DropdownBox(view.OptionArea, "RepeatDropdown", options, 16);
            dd.onValueChanged.AddListener(v => view.Instance.OptionB = v);
        }

        /// <summary>
        /// 오른쪽 결과 화면 구성: 탄소 발자국 + 게이지 + 반복 상황.
        /// </summary>
        /// <param name="area">결과 영역 루트</param>
        protected override void BuildResultArea(RectTransform area)
        {
            // 반복 실행 상황 (상단)
            _repeatText = UIFactory.Label(area, "RepeatText", "", 20);
            var repRt = (RectTransform)_repeatText.transform;
            repRt.anchorMin = new Vector2(0.05f, 0.85f);
            repRt.anchorMax = new Vector2(0.95f, 0.97f);
            _repeatText.color = new Color(0.7f, 0.9f, 1f);

            // 탄소 발자국 (가운데 - 수치에 따라 스케일 변화)
            _footprint = UIFactory.Panel(area, "Footprint",
                new Vector2(0.35f, 0.42f), new Vector2(0.65f, 0.8f), new Color(0.35f, 0.3f, 0.28f));
            _footprint.raycastTarget = false;
            _footprintLabel = UIFactory.Label(_footprint.transform, "FootprintLabel", "탄소\n발자국", 18);

            // 탄소 게이지
            var barBg = UIFactory.Panel(area, "CarbonBarBg",
                new Vector2(0.1f, 0.25f), new Vector2(0.9f, 0.35f), new Color(0, 0, 0, 0.35f));
            barBg.raycastTarget = false;
            _carbonBar = UIFactory.Panel(barBg.transform, "CarbonBar",
                new Vector2(0, 0), new Vector2(0.5f, 1), new Color(0.5f, 0.45f, 0.4f));
            _carbonBar.raycastTarget = false;

            // 탄소 수치 텍스트
            _carbonText = UIFactory.Label(area, "CarbonText", "", 20);
            var carbRt = (RectTransform)_carbonText.transform;
            carbRt.anchorMin = new Vector2(0.05f, 0.05f);
            carbRt.anchorMax = new Vector2(0.95f, 0.2f);

            ResetResultView();
        }

        /// <summary>결과 화면을 시작 탄소 수치로 되돌린다.</summary>
        public override void ResetResultView()
        {
            _displayCarbon = Data.GetParamFloat("StartCarbon", 30f);
            _repeatText.text = "";
            ApplyCarbonVisual(_displayCarbon);
        }

        /// <summary>
        /// 프로그램 실행: 블록마다 정한 횟수만큼 행동을 반복하며
        /// 탄소 발자국을 갱신한다. (반복문의 동작을 한 회씩 시각화)
        /// </summary>
        /// <param name="program">조립된 블록 목록</param>
        public override IEnumerator RunProgram(List<BlockInstance> program)
        {
            float start = Data.GetParamFloat("StartCarbon", 30f);
            float carbon = start;
            _displayCarbon = carbon;
            ApplyCarbonVisual(carbon);

            foreach (var block in program)
            {
                int repeat = block.OptionB + 1;   // 드롭다운 인덱스 → 실제 횟수

                // 반복문 시각화: 같은 행동을 횟수만큼 한 회씩 실행한다
                for (int i = 1; i <= repeat; i++)
                {
                    SetStatus($"{block.Entry.Name} 반복 실행 중...");
                    _repeatText.text = $"{block.Entry.Name}  {i} / {repeat}회";

                    carbon = Mathf.Max(0, carbon + block.Entry.Value);
                    yield return AnimateCarbonTo(carbon);

                    // 반복 한 회 사이의 짧은 간격 (애니메이션 꺼짐 시 생략)
                    if (Core.DataManager.Config.UseAnimation)
                        yield return new WaitForSeconds(0.15f);
                }
                yield return StepWait;
            }

            _repeatText.text = "모든 행동 완료!";

            // ----- 점수 계산: 탄소 감소량 기준, 0점 이상 (Episode4.json Params) -----
            // ScorePerReduce 만큼 줄일 때마다 1점 (기본: 5 감소 = 1점)
            float reduce = Mathf.Max(0f, start - carbon);
            float per = Mathf.Max(1f, Data.GetParamFloat("ScorePerReduce", 5f));
            int score = Mathf.FloorToInt(reduce / per);

            // 탄소 발자국이 시작보다 줄었으면 성공으로 판정한다
            Finish(score, carbon < start);
        }

        /// <summary>
        /// 탄소 표시값을 목표치까지 부드럽게 보간하는 애니메이션 코루틴.
        /// </summary>
        /// <param name="target">목표 탄소 수치</param>
        private IEnumerator AnimateCarbonTo(float target)
        {
            if (!Core.DataManager.Config.UseAnimation)
            {
                _displayCarbon = target;
                ApplyCarbonVisual(target);
                yield break;
            }

            float from = _displayCarbon;
            const float duration = 0.25f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _displayCarbon = Mathf.Lerp(from, target, t / duration);
                ApplyCarbonVisual(_displayCarbon);
                yield return null;
            }
            _displayCarbon = target;
            ApplyCarbonVisual(target);
        }

        /// <summary>
        /// 탄소 수치를 화면에 반영한다: 발자국 크기/색, 게이지, 수치 텍스트.
        /// </summary>
        /// <param name="carbon">표시할 탄소 수치</param>
        private void ApplyCarbonVisual(float carbon)
        {
            // 0~60 범위를 기준으로 시각화한다 (시작 30 의 2배까지 표현)
            float ratio = Mathf.Clamp01(carbon / 60f);

            // 발자국: 탄소가 많을수록 크고 어두워진다 (0.5~1.4배)
            float scale = Mathf.Lerp(0.5f, 1.4f, ratio);
            _footprint.rectTransform.localScale = new Vector3(scale, scale, 1f);
            _footprint.color = Color.Lerp(new Color(0.45f, 0.6f, 0.4f), new Color(0.2f, 0.15f, 0.12f), ratio);

            // 게이지와 수치
            _carbonBar.rectTransform.anchorMax = new Vector2(ratio, 1);
            _carbonBar.color = Color.Lerp(new Color(0.4f, 0.75f, 0.45f), new Color(0.6f, 0.35f, 0.25f), ratio);
            _carbonText.text = $"탄소 발자국: {carbon:0}";
            _footprintLabel.text = $"탄소\n발자국\n{carbon:0}";
        }
    }
}
