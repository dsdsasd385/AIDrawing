using System;
using System.Collections.Generic;

namespace EarthCoding.Data
{
    // ============================================================
    // 외부 JSON 데이터와 1:1 로 대응하는 직렬화 클래스 모음.
    // 작업계획서 14~15장 (외부 데이터 관리 / JSON 기반 데이터 관리) 대응.
    // 운영자는 Data 폴더의 JSON 파일만 수정하면 되고,
    // 프로그램은 실행 시 DataManager 가 이 클래스들로 역직렬화한다.
    // ============================================================

    /// <summary>
    /// 스토리 시작/종료 문구 데이터. (Data/Story.json)
    /// 스토리 시스템(StoryScreen)에서 인트로/클로징 화면에 표시된다.
    /// </summary>
    [Serializable]
    public class StoryData
    {
        /// <summary>인트로 화면 제목</summary>
        public string IntroTitle = "지구를 지켜줘!";

        /// <summary>인트로 화면 본문 문구 (\n 으로 줄바꿈)</summary>
        public string IntroText = "지구가 점점 뜨거워지고 있어요.\n여러분의 코딩으로 지구를 지켜주세요!";

        /// <summary>클로징 화면 제목</summary>
        public string OutroTitle = "고마워요!";

        /// <summary>클로징 화면 본문 문구</summary>
        public string OutroText = "여러분 덕분에 지구가 건강해졌어요.\n오늘 배운 것을 생활 속에서도 실천해 보세요!";
    }

    /// <summary>
    /// 블록 한 개의 정의 데이터. (각 Episode JSON 의 Blocks 배열 요소)
    /// 블록 시스템(BlockPalette)이 이 데이터를 읽어 왼쪽 블록 선택 영역을 구성한다.
    /// </summary>
    [Serializable]
    public class BlockEntry
    {
        /// <summary>블록 고유 ID (에피소드 로직에서 블록을 구분하는 키)</summary>
        public string Id;

        /// <summary>화면에 표시되는 블록 이름</summary>
        public string Name;

        /// <summary>
        /// 블록 종류. 블록의 모양/색상을 결정한다.
        /// Start / End / Command / Variable / Condition / Loop / Function 중 하나.
        /// </summary>
        public string Type = "Command";

        /// <summary>힌트/설명 팝업에 표시되는 블록 설명</summary>
        public string Description = "";

        /// <summary>
        /// 블록의 효과 값. 에피소드마다 의미가 다르다.
        /// 예) Episode1: '늘리기' 선택 시 CO₂ 변화량 / Episode4: 1회당 탄소 발자국 변화량
        /// </summary>
        public float Value;

        /// <summary>
        /// 추가 문자열 파라미터. 에피소드마다 의미가 다르다.
        /// 예) Episode2: 정답 순서 인덱스 / Episode3: 재난-대응 정답 매핑
        /// </summary>
        public string Extra = "";
    }

    /// <summary>
    /// 키-값 형태의 에피소드 세부 파라미터. (각 Episode JSON 의 Params 배열 요소)
    /// 점수 기준, 성공 조건 등 숫자/문자 설정을 코드 수정 없이 조정할 때 사용한다.
    /// </summary>
    [Serializable]
    public class ParamEntry
    {
        /// <summary>파라미터 이름</summary>
        public string Key;

        /// <summary>파라미터 값 (문자열로 저장하고 사용처에서 형변환)</summary>
        public string Value;
    }

    /// <summary>
    /// 에피소드 1개의 전체 데이터. (Data/Episode{n}.json)
    /// 제목, 설명, 힌트, 블록 구성, 점수 기준을 모두 포함하는 데이터 중심 구조의 핵심.
    /// EpisodeManager 와 각 에피소드 컨트롤러가 이 데이터를 읽어 화면을 구성한다.
    /// </summary>
    [Serializable]
    public class EpisodeData
    {
        /// <summary>에피소드 번호 (1~5)</summary>
        public int EpisodeId;

        /// <summary>상단 UI에 표시되는 에피소드 제목</summary>
        public string EpisodeName = "";

        /// <summary>상단 UI에 표시되는 에피소드 설명</summary>
        public string Description = "";

        /// <summary>힌트 버튼을 눌렀을 때 표시되는 문구 (정답이 아닌 방향 제시)</summary>
        public string Hint = "";

        /// <summary>실행 성공 시 결과 화면에 표시되는 문구</summary>
        public string SuccessText = "성공! 지구가 좋아하고 있어요!";

        /// <summary>실행 실패 시 결과 화면에 표시되는 문구</summary>
        public string FailText = "아쉬워요. 다시 한 번 해볼까요?";

        /// <summary>이 에피소드에서 사용하는 블록 목록 (왼쪽 팔레트 구성)</summary>
        public List<BlockEntry> Blocks = new List<BlockEntry>();

        /// <summary>점수 기준 등 에피소드별 세부 파라미터</summary>
        public List<ParamEntry> Params = new List<ParamEntry>();

        /// <summary>
        /// 파라미터 값을 문자열로 가져온다.
        /// </summary>
        /// <param name="key">파라미터 이름</param>
        /// <param name="defaultValue">키가 없을 때 사용할 기본값</param>
        /// <returns>파라미터 값 (없으면 defaultValue)</returns>
        public string GetParam(string key, string defaultValue = "")
        {
            if (Params != null)
                foreach (var p in Params)
                    if (p != null && p.Key == key) return p.Value;
            return defaultValue;
        }

        /// <summary>
        /// 파라미터 값을 float 로 가져온다. 변환 실패 시 기본값을 반환한다.
        /// </summary>
        /// <param name="key">파라미터 이름</param>
        /// <param name="defaultValue">키가 없거나 숫자가 아닐 때 기본값</param>
        public float GetParamFloat(string key, float defaultValue = 0f)
        {
            var v = GetParam(key, null);
            return float.TryParse(v, out var f) ? f : defaultValue;
        }

        /// <summary>
        /// 파라미터 값을 int 로 가져온다. 변환 실패 시 기본값을 반환한다.
        /// </summary>
        /// <param name="key">파라미터 이름</param>
        /// <param name="defaultValue">키가 없거나 숫자가 아닐 때 기본값</param>
        public int GetParamInt(string key, int defaultValue = 0)
        {
            var v = GetParam(key, null);
            return int.TryParse(v, out var i) ? i : defaultValue;
        }
    }

    /// <summary>
    /// 전시장 운영 설정 데이터. (Config/Config.json)
    /// 작업계획서 18장 '운영자 수정 가능 항목' 대응.
    /// 관리자 비밀번호, 자동 복귀 시간, 사운드 사용 여부 등을 담는다.
    /// </summary>
    [Serializable]
    public class ConfigData
    {
        /// <summary>관리자 모드 진입 비밀번호</summary>
        public string AdminPassword = "0000";

        /// <summary>미사용 시 메인 화면으로 자동 복귀하는 시간(초). 0 이하이면 비활성.</summary>
        public float IdleTimeoutSec = 180f;

        /// <summary>배경음악 사용 여부</summary>
        public bool UseBgm = true;

        /// <summary>효과음 사용 여부</summary>
        public bool UseSfx = true;

        /// <summary>결과 애니메이션 재생 여부 (false 면 즉시 결과 표시)</summary>
        public bool UseAnimation = true;

        /// <summary>에피소드 제한 시간(초). 0 이면 제한 없음.</summary>
        public float TimerSec = 0f;

        /// <summary>난이도 (Easy / Normal / Hard). 에피소드 판정 기준에 활용.</summary>
        public string Difficulty = "Normal";

        /// <summary>프로그램 버전 (관리자 모드에서 표시)</summary>
        public string Version = "1.0.0";
    }
}
