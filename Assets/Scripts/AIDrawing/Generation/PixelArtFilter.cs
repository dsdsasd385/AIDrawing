using System;
using CarDrawing.Core;
using UnityEngine;

namespace CarDrawing.Generation
{
    /// <summary>
    /// AI 결과를 진짜 픽셀 그리드에 스냅시키는 후처리 필터.
    /// SD가 그리는 "픽셀아트풍"은 그리드가 어긋나고 색이 미묘하게 뭉개지므로,
    /// 블록 평균 축소 → 채널 포스터라이즈 → 니어리스트 확대로 항상 균일한 픽셀 룩을 보장한다.
    /// 어떤 실패에도 원본을 그대로 돌려준다 (전시장 무인 운영 — 필터 때문에 죽지 않는다).
    /// </summary>
    public static class PixelArtFilter
    {
        /// <summary>스타일에 픽셀화가 설정돼 있으면(pixelateWidth > 0) 적용, 아니면 원본 반환</summary>
        public static byte[] Apply(byte[] png, StylePreset style)
        {
            if (png == null || style == null || style.pixelateWidth <= 0) return png;
            try
            {
                return Pixelate(png, style.pixelateWidth, Mathf.Max(2, style.pixelateColorLevels));
            }
            catch (Exception e)
            {
                LogManager.Error($"[PixelArtFilter] 픽셀화 실패 — 원본 사용: {e.Message}");
                return png;
            }
        }

        private static byte[] Pixelate(byte[] png, int gridWidth, int levels)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(png)) throw new Exception("PNG 디코드 실패");

            int w = src.width, h = src.height;
            int gw = Mathf.Min(gridWidth, w);
            int gh = Mathf.Max(1, Mathf.RoundToInt((float)h * gw / w));
            Color32[] pixels = src.GetPixels32();

            // 1) 블록 평균 축소 + 2) 포스터라이즈 (채널당 levels 단계로 제한 = 레트로 팔레트 느낌)
            var small = new Color32[gw * gh];
            float step = 255f / (levels - 1);
            for (int by = 0; by < gh; by++)
            {
                int y0 = by * h / gh, y1 = Mathf.Max(y0 + 1, (by + 1) * h / gh);
                for (int bx = 0; bx < gw; bx++)
                {
                    int x0 = bx * w / gw, x1 = Mathf.Max(x0 + 1, (bx + 1) * w / gw);
                    long r = 0, g = 0, b = 0;
                    int n = 0;
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++)
                        {
                            Color32 c = pixels[y * w + x];
                            r += c.r; g += c.g; b += c.b; n++;
                        }
                    small[by * gw + bx] = new Color32(
                        Quantize(r / n, step), Quantize(g / n, step), Quantize(b / n, step), 255);
                }
            }

            // 3) 니어리스트 확대 — 원본 해상도를 유지해 표시·저장·업로드 파이프라인이 크기를 신경 쓰지 않게 한다
            var outPixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int by = y * gh / h;
                for (int x = 0; x < w; x++)
                    outPixels[y * w + x] = small[by * gw + x * gw / w];
            }

            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            dst.SetPixels32(outPixels);
            dst.Apply();
            byte[] result = dst.EncodeToPNG();
            UnityEngine.Object.Destroy(src);
            UnityEngine.Object.Destroy(dst);
            return result;
        }

        private static byte Quantize(long v, float step)
            => (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Round(v / step) * step), 0, 255);
    }
}
