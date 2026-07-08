using UnityEngine;
using EarthCoding.Data;

namespace EarthCoding.Blocks
{
    /// <summary>
    /// 조립 영역에 놓인 블록 1개의 런타임 상태.
    /// BlockEntry(JSON 정의)는 '블록의 종류'이고, BlockInstance 는 '놓인 블록 하나'이다.
    /// 에피소드 실행 시 이 목록이 곧 체험자가 만든 프로그램이 된다.
    /// </summary>
    public class BlockInstance
    {
        /// <summary>이 블록의 원본 정의 (JSON 에서 읽은 데이터)</summary>
        public BlockEntry Entry;

        /// <summary>
        /// 블록의 첫 번째 선택값 (드롭다운 인덱스).
        /// Episode1: 0=늘리기 / 1=줄이기, Episode3: 재난 인덱스, Episode4: 행동엔 미사용
        /// </summary>
        public int OptionA;

        /// <summary>
        /// 블록의 두 번째 선택값.
        /// Episode3: 대응 인덱스, Episode4: 반복 횟수(1~Max)
        /// </summary>
        public int OptionB;

        /// <summary>
        /// 블록 인스턴스를 생성한다.
        /// </summary>
        /// <param name="entry">원본 블록 정의</param>
        public BlockInstance(BlockEntry entry)
        {
            Entry = entry;
        }
    }

    /// <summary>
    /// 블록 종류(Type)별 시각 스타일 정의. (작업계획서 5장 '블록 모양' 대응)
    /// 아트 리소스 적용 전까지 색상 + 모양 기호로 블록 종류를 구분한다.
    /// 리소스가 제작되면 이 클래스에서 스프라이트 매핑으로 교체한다.
    /// </summary>
    public static class BlockStyle
    {
        /// <summary>
        /// 블록 종류에 맞는 배경색을 반환한다.
        /// </summary>
        /// <param name="type">블록 종류 문자열 (BlockEntry.Type)</param>
        public static Color GetColor(string type)
        {
            switch (type)
            {
                case "Start":     return new Color(0.20f, 0.65f, 0.35f);  // 초록 - 시작
                case "End":       return new Color(0.75f, 0.30f, 0.30f);  // 빨강 - 종료
                case "Variable":  return new Color(0.55f, 0.40f, 0.80f);  // 보라 - 변수
                case "Condition": return new Color(0.90f, 0.60f, 0.20f);  // 주황 - 조건문
                case "Loop":      return new Color(0.20f, 0.60f, 0.65f);  // 청록 - 반복문
                case "Function":  return new Color(0.85f, 0.45f, 0.65f);  // 분홍 - 함수
                default:          return new Color(0.30f, 0.50f, 0.80f);  // 파랑 - 일반 명령
            }
        }

        /// <summary>
        /// 블록 종류에 맞는 모양 기호를 반환한다.
        /// 색을 구분하지 못해도 기호(형태)만으로 블록 종류를 알 수 있게 한다.
        /// </summary>
        /// <param name="type">블록 종류 문자열</param>
        public static string GetShapeSymbol(string type)
        {
            switch (type)
            {
                case "Start":     return "ㄱ";  // ㄱ 형태 - 시작 블록
                case "End":       return "ㄴ";  // 종료 블록 (시작과 짝)
                case "Variable":  return "ㄴ";  // ㄴ 형태 - 변수 블록
                case "Condition": return "ㄷ";  // ㄷ 형태 - 조건문 블록
                case "Loop":      return "U";   // U 형태 - 반복문 블록
                case "Function":  return "ƒ";   // 캡슐형 - 함수 블록
                default:          return "ㅡ";  // ㅡ 형태 - 일반 명령 블록
            }
        }
    }
}
