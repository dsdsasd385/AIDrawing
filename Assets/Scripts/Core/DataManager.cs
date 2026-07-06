using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using EarthCoding.Data;

namespace EarthCoding.Core
{
    /// <summary>
    /// 외부 데이터 관리 매니저. (작업계획서 13~15장, 20장, 23장 대응)
    /// 스토리/에피소드/설정 JSON 을 읽어 프로그램 전체에 제공하는 데이터 중심 구조의 핵심.
    ///
    /// 읽기 우선순위 (자동 복구 포함):
    ///   1) 실행 파일 옆 Data / Config 폴더  (전시장 배포 구조 - 운영자 수정용)
    ///   2) StreamingAssets/Data, StreamingAssets/Config  (프로젝트 기본 데이터)
    ///   3) 코드에 내장된 기본값(Default Data)  (파일 없음/손상 시에도 계속 동작)
    ///
    /// 파일이 없으면 기본값으로 JSON 을 자동 생성하여 운영자가 수정할 수 있게 한다.
    /// </summary>
    public static class DataManager
    {
        /// <summary>스토리 문구 데이터 (인트로/클로징)</summary>
        public static StoryData Story { get; private set; } = new StoryData();

        /// <summary>운영 설정 데이터 (비밀번호, 타이머 등)</summary>
        public static ConfigData Config { get; private set; } = new ConfigData();

        /// <summary>에피소드 번호(1~5) → 에피소드 데이터</summary>
        private static readonly Dictionary<int, EpisodeData> _episodes = new Dictionary<int, EpisodeData>();

        /// <summary>전체 에피소드 개수 (확장 시 이 값과 JSON 만 추가하면 된다)</summary>
        public const int EpisodeCount = 5;

        /// <summary>
        /// 실행 파일 옆 외부 데이터 루트 경로.
        /// 에디터에서는 프로젝트 루트, 빌드에서는 exe 가 있는 폴더.
        /// </summary>
        public static string ExternalRoot =>
            Application.isEditor
                ? Directory.GetParent(Application.dataPath).FullName
                : Path.GetDirectoryName(Application.dataPath);

        /// <summary>
        /// 모든 외부 데이터를 읽어 메모리에 올린다.
        /// 관리자 모드의 'JSON 다시 읽기' 에서도 호출되므로 여러 번 호출 가능해야 한다.
        /// </summary>
        public static void LoadAll()
        {
            // 스토리 문구
            Story = LoadOrCreate("Data", "Story.json", new StoryData());

            // 운영 설정
            Config = LoadOrCreate("Config", "Config.json", new ConfigData());

            // 에피소드 1~N. 파일이 없으면 기본 데이터를 만들어 저장한다.
            _episodes.Clear();
            for (int i = 1; i <= EpisodeCount; i++)
            {
                var ep = LoadOrCreate("Data", $"Episode{i}.json", DefaultEpisodes.Create(i));

                // JSON 이 손상되어 블록이 비어 있으면 기본 데이터로 자동 복구한다 (작업계획서 23장)
                if (ep.Blocks == null || ep.Blocks.Count == 0)
                {
                    LogManager.Write("Warning", $"Episode{i}.json 블록 데이터 없음 → 기본 데이터 사용");
                    ep = DefaultEpisodes.Create(i);
                }
                _episodes[i] = ep;
            }

            LogManager.Write("Info", "외부 데이터 로드 완료");
        }

        /// <summary>
        /// 에피소드 데이터를 가져온다. 없으면 기본 데이터를 반환한다.
        /// </summary>
        /// <param name="episodeId">에피소드 번호 (1~5)</param>
        public static EpisodeData GetEpisode(int episodeId)
        {
            if (_episodes.TryGetValue(episodeId, out var ep)) return ep;

            // 데이터가 없어도 프로그램이 멈추지 않도록 기본값 반환 (오류 대응)
            LogManager.Write("Warning", $"Episode{episodeId} 데이터 없음 → 기본 데이터 반환");
            return DefaultEpisodes.Create(episodeId);
        }

        /// <summary>
        /// JSON 파일을 읽고, 없으면 기본값으로 파일을 만들어 반환한다.
        /// 읽기 실패(손상 등) 시에는 기본값을 반환하여 프로그램이 계속 동작하게 한다.
        /// </summary>
        /// <typeparam name="T">역직렬화 대상 타입</typeparam>
        /// <param name="subFolder">외부 루트 하위 폴더 이름 (Data / Config)</param>
        /// <param name="fileName">파일 이름 (예: Story.json)</param>
        /// <param name="defaultValue">파일이 없거나 손상됐을 때 사용할 기본값</param>
        private static T LoadOrCreate<T>(string subFolder, string fileName, T defaultValue) where T : class
        {
            // 1순위: 실행 파일 옆 외부 폴더 (운영자 수정 대상)
            var externalPath = Path.Combine(ExternalRoot, subFolder, fileName);
            // 2순위: StreamingAssets (빌드에 포함되는 기본 데이터)
            var streamingPath = Path.Combine(Application.streamingAssetsPath, subFolder, fileName);

            foreach (var path in new[] { externalPath, streamingPath })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<T>(json);
                    if (data != null) return data;
                }
                catch (Exception e)
                {
                    // JSON 손상 시 로그만 남기고 다음 후보/기본값으로 진행한다
                    LogManager.Write("Error", $"{fileName} 읽기 실패: {e.Message}");
                }
            }

            // 파일이 어디에도 없으면 운영자가 수정할 수 있도록 외부 폴더에 자동 생성한다
            TryCreateDefaultFile(externalPath, defaultValue);
            return defaultValue;
        }

        /// <summary>
        /// 기본값 객체를 JSON 파일로 저장한다. 실패해도 프로그램은 계속 동작한다.
        /// </summary>
        /// <param name="path">저장할 파일 절대 경로</param>
        /// <param name="defaultValue">저장할 기본값 객체</param>
        private static void TryCreateDefaultFile<T>(string path, T defaultValue)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonUtility.ToJson(defaultValue, true));
                LogManager.Write("Info", $"기본 데이터 파일 자동 생성: {path}");
            }
            catch (Exception e)
            {
                LogManager.Write("Warning", $"기본 데이터 파일 생성 실패: {e.Message}");
            }
        }
    }
}
