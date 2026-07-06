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
    /// Episode 3 「자연재해 대응하기」 컨트롤러. 학습 요소: 조건문. (작업계획서 6장)
    /// 체험자는 조건문 블록 「만약 [재난]이라면 [대응]」 을 여러 개 조립한다.
    /// 실행하면 랜덤으로 재난이 발생하고, 조건이 맞는 블록이 있으면 구조 로봇이
    /// 그 대응을 실행한다. 재난에 맞는 올바른 대응이면 구조 성공.
    /// 재난/대응 목록과 정답 매핑(Answer_재난)은 Episode3.json Params 로 관리한다.
    /// 점수: 구조 성공 횟수 기준 1 / 3 / 5점.
    /// </summary>
    public class Episode3Controller : EpisodeBase
    {
        // ---------- 오른쪽 결과 화면 요소 ----------

        /// <summary>마을 패널 (평화=초록 / 재난 발생=빨강 계열로 변화)</summary>
        private Image _village;

        /// <summary>마을 상태 라벨</summary>
        private Text _villageLabel;

        /// <summary>현재 발생한 재난 표시 텍스트</summary>
        private Text _disasterText;

        /// <summary>구조 로봇의 행동 표시 텍스트</summary>
        private Text _robotText;

        /// <summary>구조 성공/실패 누적 표시</summary>
        private Text _resultCountText;

        /// <summary>재난 이름 목록 (JSON Params "Disasters" 를 | 로 분리)</summary>
        private string[] _disasters;

        /// <summary>대응 이름 목록 (JSON Params "Actions" 를 | 로 분리)</summary>
        private string[] _actions;

        /// <summary>
        /// 에피소드 시작 시 재난/대응 목록을 JSON 에서 파싱한다.
        /// (블록 드롭다운과 실행 판정 양쪽에서 사용)
        /// </summary>
        protected override void OnBegin()
        {
            _disasters = Data.GetParam("Disasters", "폭염|지진|태풍|산사태|해일|산불").Split('|');
            _actions = Data.GetParam("Actions", "물 공급|대피 안내|시설물 관리|소방 드론").Split('|');
        }

        /// <summary>
        /// 조건문 블록에 [재난] / [대응] 드롭다운 2개를 붙인다.
        /// 만약 (OptionA=재난) 이라면 (OptionB=대응) 구조가 된다.
        /// </summary>
        /// <param name="view">방금 조립된 블록</param>
        protected override void DecorateBlock(BlockView view)
        {
            // 왼쪽 절반: 재난 선택
            var disasterHolder = UIFactory.Rect(view.OptionArea, "DisasterHolder",
                new Vector2(0f, 0f), new Vector2(0.48f, 1f));
            var ddDisaster = UIFactory.DropdownBox(disasterHolder, "DisasterDropdown", _disasters, 16);
            ddDisaster.onValueChanged.AddListener(v => view.Instance.OptionA = v);

            // 오른쪽 절반: 대응 선택
            var actionHolder = UIFactory.Rect(view.OptionArea, "ActionHolder",
                new Vector2(0.52f, 0f), new Vector2(1f, 1f));
            var ddAction = UIFactory.DropdownBox(actionHolder, "ActionDropdown", _actions, 16);
            ddAction.onValueChanged.AddListener(v => view.Instance.OptionB = v);
        }

        /// <summary>
        /// 오른쪽 결과 화면 구성: 마을 + 재난 표시 + 구조 로봇 상태.
        /// </summary>
        /// <param name="area">결과 영역 루트</param>
        protected override void BuildResultArea(RectTransform area)
        {
            // 재난 발생 표시 (상단 큰 글씨)
            _disasterText = UIFactory.Label(area, "DisasterText", "", 26);
            var disRt = (RectTransform)_disasterText.transform;
            disRt.anchorMin = new Vector2(0.05f, 0.82f);
            disRt.anchorMax = new Vector2(0.95f, 0.97f);
            _disasterText.fontStyle = FontStyle.Bold;

            // 마을 (재난에 따라 색이 변한다)
            _village = UIFactory.Panel(area, "Village",
                new Vector2(0.25f, 0.4f), new Vector2(0.75f, 0.78f), new Color(0.45f, 0.75f, 0.45f));
            _village.raycastTarget = false;
            _villageLabel = UIFactory.Label(_village.transform, "VillageLabel", "평화로운 마을", 20);

            // 구조 로봇 행동 표시
            _robotText = UIFactory.Label(area, "RobotText", "", 20);
            var robotRt = (RectTransform)_robotText.transform;
            robotRt.anchorMin = new Vector2(0.05f, 0.2f);
            robotRt.anchorMax = new Vector2(0.95f, 0.36f);
            _robotText.color = new Color(0.7f, 0.9f, 1f);

            // 구조 성공 누적 카운트
            _resultCountText = UIFactory.Label(area, "ResultCount", "", 20);
            var cntRt = (RectTransform)_resultCountText.transform;
            cntRt.anchorMin = new Vector2(0.05f, 0.03f);
            cntRt.anchorMax = new Vector2(0.95f, 0.17f);

            ResetResultView();
        }

        /// <summary>결과 화면을 평화로운 마을 상태로 되돌린다.</summary>
        public override void ResetResultView()
        {
            _village.color = new Color(0.45f, 0.75f, 0.45f);
            _villageLabel.text = "평화로운 마을";
            _disasterText.text = "실행하면 재난이 발생해요!";
            _robotText.text = "";
            _resultCountText.text = "";
        }

        /// <summary>
        /// 실행 전 검사: 조건문 블록이 1개 이상 있어야 로봇이 판단할 수 있다.
        /// </summary>
        /// <param name="program">조립된 블록 목록</param>
        public override string ValidateProgram(List<BlockInstance> program)
        {
            var baseError = base.ValidateProgram(program);
            if (baseError != null) return baseError;

            // 같은 재난에 대한 조건이 중복되면 첫 번째 것만 사용됨을 미리 안내한다
            var duplicated = program.GroupBy(b => b.OptionA).Any(g => g.Count() > 1);
            if (duplicated)
                return "같은 재난의 '만약' 블록이 두 개 이상 있어요.\n재난마다 하나씩만 만들어 주세요!";
            return null;
        }

        /// <summary>
        /// 프로그램 실행: 랜덤 재난을 EventCount 회 발생시키고,
        /// 조건문 블록의 일치 여부 → 대응의 정답 여부를 판정한다.
        /// </summary>
        /// <param name="program">조립된 조건문 블록 목록</param>
        public override IEnumerator RunProgram(List<BlockInstance> program)
        {
            ResetResultView();

            int eventCount = Data.GetParamInt("EventCount", 3);
            int success = 0;

            // 재난이 중복되지 않도록 셔플 후 앞에서부터 사용한다
            var pool = Enumerable.Range(0, _disasters.Length)
                .OrderBy(_ => Random.value)
                .Take(eventCount)
                .ToList();

            for (int i = 0; i < pool.Count; i++)
            {
                int disasterIdx = pool[i];
                string disaster = _disasters[disasterIdx];

                // ----- 1) 재난 발생 연출 -----
                SetStatus($"{i + 1}번째 재난 발생!");
                _disasterText.text = $"⚠ {disaster} 발생!";
                _village.color = new Color(0.85f, 0.4f, 0.3f);
                _villageLabel.text = $"{disaster}!";
                _robotText.text = "구조 로봇: 조건 확인 중...";
                yield return StepWait;

                // ----- 2) 조건문 판정: 이 재난과 일치하는 '만약' 블록 찾기 -----
                var matched = program.FirstOrDefault(b => b.OptionA == disasterIdx);

                bool saved = false;
                if (matched == null)
                {
                    // 조건 불일치: 로봇이 어떤 대응도 하지 못한다
                    _robotText.text = $"구조 로봇: '{disaster}' 조건 블록이 없어요...";
                }
                else
                {
                    string action = _actions[matched.OptionB];
                    // 정답 매핑은 JSON 의 Answer_{재난} 파라미터로 관리한다
                    string answer = Data.GetParam($"Answer_{disaster}", "");
                    saved = action == answer;
                    _robotText.text = $"구조 로봇: {action} 실행!" + (saved ? " (구조 성공!)" : " (맞지 않는 대응...)");
                }

                yield return StepWait;

                // ----- 3) 결과 반영 연출 -----
                if (saved)
                {
                    success++;
                    _village.color = new Color(0.45f, 0.75f, 0.45f);
                    _villageLabel.text = "마을을 지켰어요!";
                }
                else
                {
                    _village.color = new Color(0.55f, 0.45f, 0.4f);
                    _villageLabel.text = "마을이 피해를 입었어요";
                }
                _resultCountText.text = $"구조 성공: {success} / {pool.Count}";
                yield return StepWait;
            }

            _disasterText.text = "모든 재난 종료!";

            // ----- 점수 계산: 성공 횟수 기준 1/3/5점 (Episode3.json Params) -----
            int score;
            if (success >= Data.GetParamInt("Score5Success", 3)) score = 5;
            else if (success >= Data.GetParamInt("Score3Success", 2)) score = 3;
            else if (success >= Data.GetParamInt("Score1Success", 1)) score = 1;
            else score = 0;

            // 3점 이상(2회 이상 구조)을 성공으로 판정한다
            Finish(score, score >= 3);
        }
    }
}
