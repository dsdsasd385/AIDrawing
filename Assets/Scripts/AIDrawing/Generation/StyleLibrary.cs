using System;
using System.Collections.Generic;
using System.IO;
using CarDrawing.Core;
using UnityEngine;

namespace CarDrawing.Generation
{
    /// <summary>
    /// 스타일 프리셋 1종. 계획서 8장: 스타일 = (프롬프트 + 디노이즈) 쌍이며 JSON 데이터로 관리한다.
    /// 기본값은 마일스톤 ①에서 검증된 실사 스타일 (Styles.json이 없어도 이 값으로 동작).
    /// </summary>
    [Serializable]
    public class StylePreset
    {
        /// <summary>스타일 식별자 (파일명·로그용)</summary>
        public string id = "realistic";
        /// <summary>버튼에 표시할 이름</summary>
        public string name = "실사";
        /// <summary>긍정 프롬프트 (워크플로 노드 3에 치환)</summary>
        public string prompt = "a photorealistic car, studio lighting, glossy paint, showroom background, high quality, detailed, 4k";
        /// <summary>부정 프롬프트 (워크플로 노드 4에 치환)</summary>
        public string negativePrompt = "sketch, drawing, cartoon, blurry, lowres, bad anatomy, text, watermark, deformed wheels";
        /// <summary>디노이즈 강도. 낮으면 낙서 충실, 높으면 색·형태가 날아감 (인수인계 §5 튜닝 1순위)</summary>
        public float denoise = 0.7f;
        /// <summary>스타일 버튼 썸네일 파일명 (디자인 리소스 확보 후 사용 예정)</summary>
        public string thumbnail = "";
    }

    /// <summary>
    /// Styles.json 로더. 생성 시스템에 속하며 StylePanel(버튼 목록)과
    /// ComfyUIClient(프롬프트·디노이즈 치환)가 함께 사용한다.
    /// 파일이 없거나 깨져도 기본 실사 1종으로 계속 동작한다 (계획서 12장).
    /// </summary>
    public static class StyleLibrary
    {
        // JsonUtility는 최상위 배열을 못 읽으므로 래퍼 클래스로 감싼다
        [Serializable]
        private class StylePresetList
        {
            public StylePreset[] styles;
        }

        private static List<StylePreset> _styles;

        /// <summary>사용 가능한 스타일 목록. 항상 1개 이상을 보장한다</summary>
        public static IReadOnlyList<StylePreset> Styles
        {
            get
            {
                EnsureLoaded();
                return _styles;
            }
        }

        /// <summary>관리자 모드에서 JSON을 다시 읽을 때 사용한다 (계획서 11장).</summary>
        public static void Reload() => _styles = null;

        private static void EnsureLoaded()
        {
            if (_styles != null) return;

            string path = Path.Combine(Application.streamingAssetsPath, "Data", "Styles.json");
            try
            {
                var list = JsonUtility.FromJson<StylePresetList>(File.ReadAllText(path));
                if (list?.styles != null && list.styles.Length > 0)
                {
                    _styles = new List<StylePreset>(list.styles);
                    return;
                }
                LogManager.Warn($"[StyleLibrary] Styles.json에 스타일이 없음 — 기본 스타일 사용: {path}");
            }
            catch (Exception e)
            {
                LogManager.Error($"[StyleLibrary] Styles.json 로드 실패 — 기본 스타일 사용: {e.Message}");
            }
            // 폴백: 검증된 실사 스타일 1종 (StylePreset 필드 초기값)
            _styles = new List<StylePreset> { new StylePreset() };
        }
    }
}
