using System.Collections.Generic;
using UnityEngine;

namespace EarthCoding.Core
{
    /// <summary>
    /// 점수 관리 매니저. (작업계획서 7장 '점수 시스템' 대응)
    /// 에피소드별 독립 점수를 보관하고 PlayerPrefs 에 저장/복원한다.
    /// 관리자 모드의 '점수 확인 / 전체 점수 초기화' 기능이 이 클래스를 사용한다.
    /// </summary>
    public static class ScoreManager
    {
        /// <summary>에피소드 ID → 획득 점수 (세션 + PlayerPrefs 동기화)</summary>
        private static readonly Dictionary<int, int> _scores = new Dictionary<int, int>();

        /// <summary>PlayerPrefs 저장 키 접두어 (다른 프로그램과 충돌 방지)</summary>
        private const string PrefKey = "EarthCoding.Score.";

        /// <summary>
        /// 저장된 점수를 PlayerPrefs 에서 읽어 메모리에 올린다. 프로그램 시작 시 1회 호출.
        /// </summary>
        public static void Load()
        {
            _scores.Clear();
            for (int i = 1; i <= DataManager.EpisodeCount; i++)
                if (PlayerPrefs.HasKey(PrefKey + i))
                    _scores[i] = PlayerPrefs.GetInt(PrefKey + i);
        }

        /// <summary>
        /// 에피소드 점수를 저장한다. 재도전 시 점수가 낮아지지 않도록 최고 점수만 유지한다.
        /// </summary>
        /// <param name="episodeId">에피소드 번호 (1~5)</param>
        /// <param name="score">이번 실행에서 획득한 점수</param>
        public static void SetScore(int episodeId, int score)
        {
            // 기존 점수보다 높을 때만 갱신 (최고 기록 유지)
            if (_scores.TryGetValue(episodeId, out var prev) && prev >= score) return;

            _scores[episodeId] = score;
            PlayerPrefs.SetInt(PrefKey + episodeId, score);
            PlayerPrefs.Save();
            LogManager.Write("Info", $"Episode{episodeId} 점수 저장: {score}점");
        }

        /// <summary>
        /// 에피소드의 저장된 점수를 가져온다.
        /// </summary>
        /// <param name="episodeId">에피소드 번호</param>
        /// <returns>저장된 점수 (기록이 없으면 0)</returns>
        public static int GetScore(int episodeId) =>
            _scores.TryGetValue(episodeId, out var s) ? s : 0;

        /// <summary>전체 에피소드 점수 합계. 클로징 화면에서 표시한다.</summary>
        public static int TotalScore
        {
            get
            {
                int sum = 0;
                foreach (var v in _scores.Values) sum += v;
                return sum;
            }
        }

        /// <summary>
        /// 모든 점수를 초기화한다. 관리자 모드 '전체 점수 초기화' 및
        /// 체험 종료 후 다음 체험자를 위한 전체 초기화에서 사용한다.
        /// </summary>
        public static void ResetAll()
        {
            for (int i = 1; i <= DataManager.EpisodeCount; i++)
                PlayerPrefs.DeleteKey(PrefKey + i);
            PlayerPrefs.Save();
            _scores.Clear();
            LogManager.Write("Info", "전체 점수 초기화");
        }
    }
}
