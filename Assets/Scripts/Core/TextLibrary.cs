using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CarDrawing.Core
{
    /// <summary>
    /// 화면 문구 사전. StreamingAssets/Data/Texts.json(평면 키-값)을 읽는다.
    /// 코어 시스템에 속하며 모든 UI가 문구를 여기서 가져온다 (계획서 13장: 하드코딩 금지).
    /// 파일이 없거나 키가 빠져도 키 문자열 자체를 돌려주어 화면이 비지 않게 한다.
    /// </summary>
    public static class TextLibrary
    {
        private static Dictionary<string, string> _texts;

        /// <summary>
        /// 키에 해당하는 화면 문구를 돌려준다.
        /// </summary>
        /// <param name="key">Texts.json의 키 (예: "attract.start")</param>
        /// <returns>문구. 키가 없으면 키 자체 (빈 화면 방지 폴백)</returns>
        public static string Get(string key)
        {
            EnsureLoaded();
            if (_texts.TryGetValue(key, out string value)) return value;

            LogManager.Warn($"[TextLibrary] 문구 키 없음: {key}");
            _texts[key] = key; // 같은 키로 경고가 반복되지 않도록 폴백을 등록해 둔다
            return key;
        }

        /// <summary>관리자 모드에서 JSON을 다시 읽을 때 사용한다 (계획서 11장).</summary>
        public static void Reload() => _texts = null;

        private static void EnsureLoaded()
        {
            if (_texts != null) return;
            _texts = new Dictionary<string, string>();

            string path = Path.Combine(Application.streamingAssetsPath, "Data", "Texts.json");
            try
            {
                foreach (JProperty prop in JObject.Parse(File.ReadAllText(path)).Properties())
                    _texts[prop.Name] = (string)prop.Value;
            }
            catch (Exception e)
            {
                // 파일이 없어도 키 폴백으로 계속 동작한다 (계획서 12장: 예외로 죽지 않기)
                LogManager.Error($"[TextLibrary] Texts.json 로드 실패: {e.Message}");
            }
        }
    }
}
