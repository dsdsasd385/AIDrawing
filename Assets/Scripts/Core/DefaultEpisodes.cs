using System.Collections.Generic;
using EarthCoding.Data;

namespace EarthCoding.Core
{
    /// <summary>
    /// 에피소드 기본 데이터(Default Data) 생성기. (작업계획서 20장, 23장 대응)
    /// JSON 파일이 없거나 손상되었을 때 프로그램이 계속 동작하도록
    /// 5개 에피소드의 기본 콘텐츠를 코드에 내장한다.
    /// 최초 실행 시 이 데이터가 JSON 파일로 자동 생성되어 운영자 수정의 출발점이 된다.
    /// </summary>
    public static class DefaultEpisodes
    {
        /// <summary>
        /// 에피소드 번호에 해당하는 기본 데이터를 생성한다.
        /// </summary>
        /// <param name="episodeId">에피소드 번호 (1~5)</param>
        /// <returns>기본 콘텐츠가 채워진 에피소드 데이터</returns>
        public static EpisodeData Create(int episodeId)
        {
            switch (episodeId)
            {
                case 1: return Episode1();
                case 2: return Episode2();
                case 3: return Episode3();
                case 4: return Episode4();
                case 5: return Episode5();
                default:
                    // 정의되지 않은 번호가 와도 빈 데이터로 계속 동작하게 한다
                    return new EpisodeData { EpisodeId = episodeId, EpisodeName = $"에피소드 {episodeId}" };
            }
        }

        /// <summary>
        /// Episode 1 「지구 온도 낮추기」 - 학습 요소: 변수
        /// 블록마다 늘리기/줄이기를 선택하면 Value 가 CO₂ 수치에 반영된다.
        /// Value = '늘리기' 선택 시 CO₂ 변화량 ('줄이기'는 부호 반전)
        /// </summary>
        private static EpisodeData Episode1()
        {
            return new EpisodeData
            {
                EpisodeId = 1,
                EpisodeName = "지구 온도 낮추기",
                Description = "블록의 늘리기/줄이기(변수)를 정해서 CO₂를 줄이고 지구 온도를 낮춰보세요.",
                Hint = "공장 연기가 나오는 것은 줄이고,\n깨끗한 에너지와 나무는 늘려보세요!",
                SuccessText = "지구가 시원해졌어요! 고마워요!",
                FailText = "지구가 아직 뜨거워요. 블록의 늘리기/줄이기를 바꿔볼까요?",
                Blocks = new List<BlockEntry>
                {
                    new BlockEntry { Id = "coal",  Name = "화력 발전",   Type = "Variable", Value = 20f,
                        Description = "석탄을 태워 전기를 만들어요. 늘리면 CO₂가 많이 나와요." },
                    new BlockEntry { Id = "ev",    Name = "전기 자동차", Type = "Variable", Value = -10f,
                        Description = "매연이 나오지 않는 자동차예요. 늘리면 공기가 깨끗해져요." },
                    new BlockEntry { Id = "park",  Name = "공원",       Type = "Variable", Value = -15f,
                        Description = "나무가 CO₂를 마시고 산소를 만들어요." },
                    new BlockEntry { Id = "dust",  Name = "미세먼지",    Type = "Variable", Value = 10f,
                        Description = "하늘을 뿌옇게 만드는 작은 먼지예요." },
                },
                Params = new List<ParamEntry>
                {
                    // 시작 CO₂ 수치와 점수 판정 기준 (운영자 조정 가능)
                    new ParamEntry { Key = "StartCo2",   Value = "100" },
                    new ParamEntry { Key = "Score3Co2",  Value = "45" },   // 이하이면 3점
                    new ParamEntry { Key = "Score2Co2",  Value = "70" },   // 이하이면 2점
                    new ParamEntry { Key = "Score1Co2",  Value = "99" },   // 이하이면 1점 (그 외 0점)
                },
            };
        }

        /// <summary>
        /// Episode 2 「빙하 지키기」 - 학습 요소: 명령 순서 (순차 실행)
        /// Extra = 올바른 실행 순서 (1부터 시작). 순서가 맞을수록 점수가 높다.
        /// </summary>
        private static EpisodeData Episode2()
        {
            return new EpisodeData
            {
                EpisodeId = 2,
                EpisodeName = "빙하 지키기",
                Description = "탐사 로봇에게 올바른 순서로 명령을 내려 빙하를 지켜주세요.",
                Hint = "먼저 빙하를 살펴보고(측정),\n마지막에 북극곰을 지켜주는 순서로 해보세요!",
                SuccessText = "빙하와 북극곰을 지켜냈어요!",
                FailText = "순서가 조금 아쉬워요. 측정을 먼저 해볼까요?",
                Blocks = new List<BlockEntry>
                {
                    new BlockEntry { Id = "thick",  Name = "빙하 두께 측정",   Type = "Command", Extra = "1",
                        Description = "빙하가 얼마나 두꺼운지 재요. 탐사의 첫걸음!" },
                    new BlockEntry { Id = "size",   Name = "빙하 크기 측정",   Type = "Command", Extra = "2",
                        Description = "빙하가 얼마나 큰지 재요." },
                    new BlockEntry { Id = "temp",   Name = "바닷물 온도 측정", Type = "Command", Extra = "3",
                        Description = "바닷물이 얼마나 따뜻한지 재요." },
                    new BlockEntry { Id = "sample", Name = "샘플 모으기",     Type = "Command", Extra = "4",
                        Description = "측정이 끝나면 얼음 조각을 모아 연구실로 보내요." },
                    new BlockEntry { Id = "bear",   Name = "북극곰 지키기",   Type = "Command", Extra = "5",
                        Description = "마지막으로 북극곰이 안전한지 확인해요." },
                },
                Params = new List<ParamEntry>
                {
                    // 올바른 위치에 놓인 블록 개수에 따른 점수 기준
                    new ParamEntry { Key = "Score3Correct", Value = "5" },  // 5개 모두 정순서 → 3점
                    new ParamEntry { Key = "Score2Correct", Value = "3" },  // 3개 이상 → 2점
                    new ParamEntry { Key = "Score1Correct", Value = "0" },  // 그 외 → 1점 (참여 점수)
                },
            };
        }

        /// <summary>
        /// Episode 3 「자연재해 대응하기」 - 학습 요소: 조건문
        /// Type=Condition 블록: 만약 [재난]이 나타나면 [대응]을 한다.
        /// Extra = "재난ID" (Disaster* 블록) / 정답 매핑은 Params 의 Answer_* 로 관리.
        /// </summary>
        private static EpisodeData Episode3()
        {
            return new EpisodeData
            {
                EpisodeId = 3,
                EpisodeName = "자연재해 대응하기",
                Description = "만약 ~라면! 재난에 맞는 대응을 정해서 구조 로봇을 움직여 보세요.",
                Hint = "불이 나면 무엇이 필요할까요?\n뜨거운 날에는 무엇이 필요할까요?",
                SuccessText = "구조 로봇이 마을을 지켜냈어요!",
                FailText = "재난에 맞는 대응을 다시 생각해 볼까요?",
                Blocks = new List<BlockEntry>
                {
                    // 조건문 블록: 사용자는 이 블록을 조립 영역에 놓고 재난/대응을 고른다
                    new BlockEntry { Id = "if", Name = "만약 ~라면", Type = "Condition",
                        Description = "만약 [재난]이 일어나면 [대응]을 해요. 조건문 블록이에요." },
                },
                Params = new List<ParamEntry>
                {
                    // 재난 목록 (표시 이름은 | 로 구분)
                    new ParamEntry { Key = "Disasters", Value = "폭염|지진|태풍|산사태|해일|산불" },
                    // 대응 목록
                    new ParamEntry { Key = "Actions",   Value = "물 공급|대피 안내|시설물 관리|소방 드론" },
                    // 재난별 정답 대응 (재난 이름 → 대응 이름)
                    new ParamEntry { Key = "Answer_폭염",   Value = "물 공급" },
                    new ParamEntry { Key = "Answer_지진",   Value = "대피 안내" },
                    new ParamEntry { Key = "Answer_태풍",   Value = "시설물 관리" },
                    new ParamEntry { Key = "Answer_산사태", Value = "대피 안내" },
                    new ParamEntry { Key = "Answer_해일",   Value = "대피 안내" },
                    new ParamEntry { Key = "Answer_산불",   Value = "소방 드론" },
                    // 실행 시 랜덤으로 발생시킬 재난 횟수
                    new ParamEntry { Key = "EventCount", Value = "3" },
                    // 성공 횟수별 점수 (3회 모두 성공 5점 / 2회 3점 / 1회 1점)
                    new ParamEntry { Key = "Score5Success", Value = "3" },
                    new ParamEntry { Key = "Score3Success", Value = "2" },
                    new ParamEntry { Key = "Score1Success", Value = "1" },
                },
            };
        }

        /// <summary>
        /// Episode 4 「탄소 배출 줄이기」 - 학습 요소: 반복문
        /// Type=Loop 반복 블록 안에 행동을 넣고 횟수를 정한다.
        /// Value = 1회 실행 시 탄소 발자국 변화량 (음수 = 줄어듦/좋은 행동)
        /// </summary>
        private static EpisodeData Episode4()
        {
            return new EpisodeData
            {
                EpisodeId = 4,
                EpisodeName = "탄소 배출 줄이기",
                Description = "좋은 행동을 반복(반복문)해서 탄소 발자국을 줄여보세요.",
                Hint = "좋은 행동은 여러 번 반복할수록\n탄소 발자국이 작아져요!",
                SuccessText = "탄소 발자국이 작아졌어요! 지구가 가벼워졌대요!",
                FailText = "탄소 발자국이 아직 커요. 좋은 행동을 더 반복해 볼까요?",
                Blocks = new List<BlockEntry>
                {
                    // 좋은 행동 (Extra=good) : 반복할수록 탄소 발자국 감소
                    new BlockEntry { Id = "walk",    Name = "걷기",          Type = "Loop", Value = -3f, Extra = "good",
                        Description = "가까운 거리는 걸어요. 매연이 나오지 않아요." },
                    new BlockEntry { Id = "recycle", Name = "재활용",        Type = "Loop", Value = -2f, Extra = "good",
                        Description = "쓰레기를 다시 쓸 수 있게 분리해요." },
                    new BlockEntry { Id = "plug",    Name = "플러그 뽑기",   Type = "Loop", Value = -2f, Extra = "good",
                        Description = "안 쓰는 전기 제품의 플러그를 뽑아요." },
                    new BlockEntry { Id = "water",   Name = "물 절약",       Type = "Loop", Value = -2f, Extra = "good",
                        Description = "물을 아껴 쓰면 에너지도 아낄 수 있어요." },
                    // 나쁜 행동 (Extra=bad) : 반복할수록 탄소 발자국 증가
                    new BlockEntry { Id = "car",     Name = "자동차",        Type = "Loop", Value = 3f, Extra = "bad",
                        Description = "가까운 거리도 자동차를 타면 매연이 나와요." },
                    new BlockEntry { Id = "disposable", Name = "일회용품",   Type = "Loop", Value = 2f, Extra = "bad",
                        Description = "한 번 쓰고 버리는 물건은 쓰레기가 돼요." },
                    new BlockEntry { Id = "power",   Name = "과다 전기 사용", Type = "Loop", Value = 2f, Extra = "bad",
                        Description = "전기를 너무 많이 쓰면 발전소가 더 일해야 해요." },
                    new BlockEntry { Id = "overwater", Name = "과다 물 사용", Type = "Loop", Value = 2f, Extra = "bad",
                        Description = "물을 낭비하면 깨끗한 물이 부족해져요." },
                },
                Params = new List<ParamEntry>
                {
                    // 시작 탄소 발자국 수치
                    new ParamEntry { Key = "StartCarbon", Value = "30" },
                    // 반복 횟수 선택 범위
                    new ParamEntry { Key = "MaxRepeat",   Value = "5" },
                    // 점수 = (시작 - 최종 탄소) 를 이 값으로 나눈 몫, 0점 이상
                    new ParamEntry { Key = "ScorePerReduce", Value = "5" },
                },
            };
        }

        /// <summary>
        /// Episode 5 「지구 회복하기」 - 학습 요소: 함수
        /// '지구 회복 프로젝트' 함수를 단계 블록으로 정의하고, 함수 호출 블록으로 실행한다.
        /// Extra = 함수 정의에 들어가야 하는 단계 순서 (1~4) / call = 함수 호출 블록
        /// </summary>
        private static EpisodeData Episode5()
        {
            return new EpisodeData
            {
                EpisodeId = 5,
                EpisodeName = "지구 회복하기",
                Description = "지구 회복 프로젝트(함수)를 완성하고, 함수를 호출해서 지구를 회복시키세요!",
                Hint = "함수 안에 네 가지 단계를 순서대로 넣고,\n함수를 여러 번 호출하면 지구가 더 많이 회복돼요!",
                SuccessText = "지구가 완전히 회복됐어요! 여러분은 지구의 영웅이에요!",
                FailText = "함수 안의 단계를 다시 확인해 볼까요?",
                Blocks = new List<BlockEntry>
                {
                    // 함수 안에 들어가는 단계 블록 (Extra = 올바른 순서)
                    new BlockEntry { Id = "carbon", Name = "탄소 줄이기",     Type = "Function", Extra = "1",
                        Description = "지구 회복 1단계! 탄소를 줄여요." },
                    new BlockEntry { Id = "energy", Name = "신재생에너지",    Type = "Function", Extra = "2",
                        Description = "지구 회복 2단계! 태양과 바람으로 전기를 만들어요." },
                    new BlockEntry { Id = "trash",  Name = "쓰레기 줄이기",   Type = "Function", Extra = "3",
                        Description = "지구 회복 3단계! 쓰레기를 줄여요." },
                    new BlockEntry { Id = "calc",   Name = "계산하기",       Type = "Function", Extra = "4",
                        Description = "지구 회복 4단계! 얼마나 회복됐는지 계산해요." },
                    // 완성된 함수를 실행하는 호출 블록
                    new BlockEntry { Id = "call",   Name = "지구 회복 프로젝트 호출", Type = "Command", Extra = "call",
                        Description = "완성한 '지구 회복 프로젝트' 함수를 실행해요." },
                },
                Params = new List<ParamEntry>
                {
                    // 함수 1회 호출 시 회복량 = 올바른 단계 수 × StepHeal
                    new ParamEntry { Key = "StepHeal",   Value = "2" },
                    // 함수 호출 블록 최대 개수 (점수 상한 관리: 4단계×2×...)
                    new ParamEntry { Key = "MaxCalls",   Value = "6" },
                    // 점수 상한 (작업계획서: 1~50점)
                    new ParamEntry { Key = "MaxScore",   Value = "50" },
                    new ParamEntry { Key = "MinScore",   Value = "1" },
                },
            };
        }
    }
}
