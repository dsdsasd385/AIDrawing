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
        /// <summary>스타일 전용 체크포인트 파일명. 실사 모델(워크플로 기본)로는 안 나오는 화풍(카툰 등)만 지정하고, 비우면 기본 모델 사용</summary>
        public string checkpoint = "";
        /// <summary>ControlNet 강도. 0이면 워크플로 기본값 유지. 카툰은 상상력이 강한 모델이라 세게(0.5) 걸어야 스케치 구도가 유지된다</summary>
        public float controlnetStrength = 0f;
        /// <summary>스타일 전용 LoRA 파일명(loras 폴더). 체크포인트 교체 없이 화풍만 얹을 때 쓴다(픽셀아트=PixelArtRedmond). 비우면 미사용.
        /// 이미지·영상 양쪽에 주입된다 — 영상도 같은 화풍을 유지해야 하기 때문(2026-07-13 픽셀 영상 품질 저하 대응, 인수인계 §6)</summary>
        public string lora = "";
        /// <summary>LoRA 강도(model=clip 동일 적용). 픽셀아트 0.8 권장(1.0은 주변 잡픽셀↑)</summary>
        public float loraStrength = 0.8f;
        /// <summary>LoRA 트리거 단어. 이미지·영상 긍정 프롬프트 앞에 붙는다 (PixelArtRedmond = "Pixel Art, PixArFK")</summary>
        public string loraTrigger = "";
        /// <summary>픽셀화 후처리의 가로 픽셀 수. 0이면 후처리 없음. LoRA로 화풍을 만들고 이 값으로 그리드만 균일하게 스냅(PixelArtFilter·영상 재픽셀화 공용)</summary>
        public int pixelateWidth = 0;
        /// <summary>픽셀화 시 채널당 색 단계 수. 낮을수록 제한 팔레트의 레트로 느낌</summary>
        public int pixelateColorLevels = 6;
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
