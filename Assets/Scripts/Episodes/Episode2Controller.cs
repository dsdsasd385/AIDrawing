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
    /// Episode 2 「빙하 지키기」 컨트롤러. 학습 요소: 명령 순서(순차 실행). (작업계획서 6장)
    /// 탐사 로봇에게 5가지 명령(두께 측정→크기 측정→온도 측정→샘플→북극곰)을
    /// 올바른 순서로 내리는 것이 목표. 블록의 정답 순서는 JSON 의 Extra(1~5)로 관리한다.
    /// 실행하면 블록이 순서대로 애니메이션되며, 올바른 위치의 명령은 빙하를 지키고
    /// 잘못된 위치의 명령은 빙하가 줄어드는 것으로 순서의 중요성을 시각화한다.
    /// 점수: 정위치 블록 개수 기준 1~3점 (기준은 Episode2.json Params).
    /// </summary>
    public class Episode2Controller : EpisodeBase
    {
        // ---------- 오른쪽 결과 화면 요소 ----------

        /// <summary>빙하 이미지 (잘못된 명령마다 줄어든다)</summary>
        private Image _ice;

        /// <summary>빙하 위에 서 있는 북극곰 (빙하가 너무 작아지면 위험 표시)</summary>
        private Image _bear;

        /// <summary>북극곰 라벨 (평소 🐻‍❄ 대신 텍스트, 리소스 적용 시 캐릭터로 교체)</summary>
        private Text _bearLabel;

        /// <summary>바닷물 온도 텍스트</summary>
        private Text _tempText;

        /// <summary>현재 실행 중인 명령 안내 텍스트</summary>
        private Text _actionText;

        /// <summary>빙하 크기 비율 (1 = 온전함, 0.4 이하 = 위험)</summary>
        private float _iceScale = 1f;

        /// <summary>바닷물 온도 표시값 (잘못된 명령마다 상승)</summary>
        private float _seaTemp;

        /// <summary>
        /// 오른쪽 결과 화면 구성: 바다 배경 + 빙하 + 북극곰 + 온도 표시.
        /// </summary>
        /// <param name="area">결과 영역 루트</param>
        protected override void BuildResultArea(RectTransform area)
        {
            // 바다 배경 (아래쪽 절반)
            var sea = UIFactory.Panel(area, "Sea",
                new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.45f), new Color(0.10f, 0.30f, 0.55f));
            sea.raycastTarget = false;

            // 빙하 (바다 위에 떠 있는 밝은 얼음 - 스케일로 크기 변화 표현)
            _ice = UIFactory.Panel(area, "Ice",
                new Vector2(0.25f, 0.38f), new Vector2(0.75f, 0.62f), new Color(0.85f, 0.95f, 1f));
            _ice.raycastTarget = false;
            UIFactory.Label(_ice.transform, "IceLabel", "빙하", 18).color = new Color(0.2f, 0.4f, 0.6f);

            // 북극곰 (빙하 위)
            _bear = UIFactory.Panel(area, "Bear",
                new Vector2(0.42f, 0.62f), new Vector2(0.58f, 0.78f), Color.white);
            _bear.raycastTarget = false;
            _bearLabel = UIFactory.Label(_bear.transform, "BearLabel", "북극곰", 15);
            _bearLabel.color = new Color(0.3f, 0.3f, 0.35f);

            // 바닷물 온도
            _tempText = UIFactory.Label(area, "TempText", "", 20);
            var tempRt = (RectTransform)_tempText.transform;
            tempRt.anchorMin = new Vector2(0.05f, 0.02f);
            tempRt.anchorMax = new Vector2(0.95f, 0.13f);

            // 현재 실행 중인 명령 표시
            _actionText = UIFactory.Label(area, "ActionText", "", 20);
            var actRt = (RectTransform)_actionText.transform;
            actRt.anchorMin = new Vector2(0.05f, 0.85f);
            actRt.anchorMax = new Vector2(0.95f, 0.97f);
            _actionText.color = new Color(0.7f, 0.9f, 1f);

            ResetResultView();
        }

        /// <summary>결과 화면을 시작 상태(온전한 빙하)로 되돌린다.</summary>
        public override void ResetResultView()
        {
            _iceScale = 1f;
            _seaTemp = -1.5f;   // 극지방 바닷물 시작 온도
            _actionText.text = "";
            ApplyIceVisual();
        }

        /// <summary>
        /// 실행 전 검사: 순서 학습이 목적이므로 최소 2개 이상의 명령이 필요하다.
        /// </summary>
        /// <param name="program">조립된 블록 목록</param>
        public override string ValidateProgram(List<BlockInstance> program)
        {
            var baseError = base.ValidateProgram(program);
            if (baseError != null) return baseError;

            if (program.Count < 2)
                return "탐사 명령을 2개 이상 조립해야 로봇이 움직일 수 있어요!";
            return null;
        }

        /// <summary>
        /// 프로그램 순차 실행: 명령을 하나씩 애니메이션하며 정위치 여부를 판정한다.
        /// 정위치 = 정답 순서(Extra 오름차순)에서 같은 인덱스에 있어야 할 블록과 일치.
        /// </summary>
        /// <param name="program">조립된 블록 목록</param>
        public override IEnumerator RunProgram(List<BlockInstance> program)
        {
            ResetResultView();

            // 정답 순서: JSON 의 Extra(1~5) 오름차순으로 정렬한 블록 ID 목록
            var answer = Data.Blocks
                .OrderBy(b => int.TryParse(b.Extra, out var o) ? o : 99)
                .Select(b => b.Id)
                .ToList();

            int correct = 0;
            for (int i = 0; i < program.Count; i++)
            {
                var block = program[i];
                SetStatus($"{i + 1}번째 명령: {block.Entry.Name}");
                _actionText.text = $"탐사 로봇: {block.Entry.Name} 실행 중...";

                // 정위치 판정: i번째 명령이 정답 순서의 i번째와 같은가
                bool isCorrect = i < answer.Count && block.Entry.Id == answer[i];

                if (isCorrect)
                {
                    correct++;
                    // 올바른 순서: 빙하가 반짝이며 유지된다
                    yield return FlashIce(new Color(0.6f, 1f, 0.9f));
                }
                else
                {
                    // 잘못된 순서: 빙하가 줄어들고 바닷물 온도가 오른다
                    _iceScale = Mathf.Max(0.3f, _iceScale - 0.15f);
                    _seaTemp += 0.8f;
                    yield return FlashIce(new Color(1f, 0.5f, 0.4f));
                }

                ApplyIceVisual();
                yield return StepWait;
            }

            _actionText.text = "탐사 완료!";

            // ----- 점수 계산: 정위치 개수 기준 (Episode2.json Params) -----
            int score;
            if (correct >= Data.GetParamInt("Score3Correct", 5)) score = 3;
            else if (correct >= Data.GetParamInt("Score2Correct", 3)) score = 2;
            else score = 1;   // 참여 점수 (작업계획서: 1~3점)

            // 만점(3점)만 '빙하 지키기 성공' 으로 판정한다
            Finish(score, score >= 3);
        }

        /// <summary>
        /// 빙하가 지정 색으로 잠깐 반짝이는 연출 코루틴. (명령 실행 피드백)
        /// </summary>
        /// <param name="flashColor">반짝일 색</param>
        private IEnumerator FlashIce(Color flashColor)
        {
            if (!Core.DataManager.Config.UseAnimation) yield break;

            var normal = new Color(0.85f, 0.95f, 1f);
            _ice.color = flashColor;
            yield return new WaitForSeconds(0.25f);
            _ice.color = normal;
        }

        /// <summary>
        /// 빙하 크기/온도/북극곰 상태를 화면에 반영한다.
        /// </summary>
        private void ApplyIceVisual()
        {
            // 빙하 크기: 스케일로 표현 (리소스 적용 시 스프라이트 교체 애니메이션으로 대체)
            _ice.rectTransform.localScale = new Vector3(_iceScale, _iceScale, 1f);

            // 북극곰은 빙하 위에 있으므로 빙하가 줄면 함께 줄어든다
            _bear.rectTransform.localScale = new Vector3(_iceScale, _iceScale, 1f);

            _tempText.text = $"바닷물 온도: {_seaTemp:0.0}℃   빙하 크기: {_iceScale * 100f:0}%";

            // 빙하가 40% 이하로 줄면 북극곰 위험 표시
            _bearLabel.text = _iceScale <= 0.4f ? "북극곰\n(위험!)" : "북극곰";
            _bear.color = _iceScale <= 0.4f ? new Color(1f, 0.75f, 0.7f) : Color.white;
        }
    }
}
