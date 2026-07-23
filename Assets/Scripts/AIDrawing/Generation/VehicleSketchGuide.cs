using System;
using UnityEngine;

namespace CarDrawing.Generation
{
    /// <summary>
    /// 바퀴가 없는 비정형 낙서에만 AI용 차대·바퀴 힌트를 더한다.
    /// 원본 PNG는 바꾸지 않고 ComfyUI에 업로드할 복사본만 생성한다.
    /// </summary>
    internal static class VehicleSketchGuide
    {
        private const byte InkThreshold = 160;
        // 작은 윤곽선 바퀴는 한쪽 밀도가 0.18 아래로 내려간다. 양쪽 모두 최소 밀도를 넘고
        // 한쪽에 확실한 바퀴가 있으면 정상 차량으로 인정해 불필요한 바퀴 추가를 막는다.
        private const float ExistingWheelDensity = 0.12f;
        private const float StrongWheelDensity = 0.20f;
        private static readonly Color32 GuideColor = new Color32(15, 15, 15, 255);

        private struct GuideLayout
        {
            public int MinX;
            public int MinY;
            public int MaxX;
            public int MaxY;
        }

        public static byte[] AddIfNeeded(byte[] linePng, out bool added)
        {
            added = false;
            if (linePng == null || linePng.Length == 0) return linePng;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(linePng, false)) return linePng;
                Color32[] pixels = texture.GetPixels32();
                if (!TryFindInkBounds(pixels, texture.width, texture.height,
                        out int minX, out int minY, out int maxX, out int maxY))
                    return linePng;

                int width = maxX - minX + 1;
                int height = maxY - minY + 1;
                if (width < 80 || height < 40 || HasLikelyWheelPair(pixels, texture.width, texture.height,
                        minX, minY, maxX, maxY))
                    return linePng;

                DrawGuides(pixels, texture.width, texture.height,
                    new GuideLayout { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY });

                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                added = true;
                return texture.EncodeToPNG();
            }
            catch (Exception)
            {
                return linePng;
            }
            finally
            {
                DestroyTexture(texture);
            }
        }

        /// <summary>선화에서 확정한 동일한 가이드를 AI용 색상 레이어 복사본에도 적용한다.</summary>
        public static byte[] AddFromReference(byte[] targetPng, byte[] referenceLinePng)
        {
            if (targetPng == null || targetPng.Length == 0 || referenceLinePng == null) return targetPng;

            var reference = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var target = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!reference.LoadImage(referenceLinePng, false) || !target.LoadImage(targetPng, false))
                    return targetPng;
                Color32[] referencePixels = reference.GetPixels32();
                if (!TryFindInkBounds(referencePixels, reference.width, reference.height,
                        out int minX, out int minY, out int maxX, out int maxY))
                    return targetPng;

                Color32[] targetPixels = target.GetPixels32();
                DrawGuides(targetPixels, target.width, target.height,
                    new GuideLayout { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY });
                target.SetPixels32(targetPixels);
                target.Apply(false, false);
                return target.EncodeToPNG();
            }
            catch (Exception)
            {
                return targetPng;
            }
            finally
            {
                DestroyTexture(reference);
                DestroyTexture(target);
            }
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(texture);
            else
                UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void DrawGuides(Color32[] pixels, int width, int height, GuideLayout layout)
        {
            int drawingWidth = layout.MaxX - layout.MinX + 1;
            int drawingHeight = layout.MaxY - layout.MinY + 1;
            int radius = Mathf.Clamp(Mathf.RoundToInt(drawingWidth * 0.085f), 18, 44);
            int leftX = Mathf.RoundToInt(Mathf.Lerp(layout.MinX, layout.MaxX, 0.2f));
            int rightX = Mathf.RoundToInt(Mathf.Lerp(layout.MinX, layout.MaxX, 0.8f));
            int centerY = Mathf.Clamp(layout.MinY - Mathf.RoundToInt(radius * 0.2f), radius + 4,
                height - radius - 4);
            int thickness = Mathf.Clamp(Mathf.RoundToInt(radius * 0.25f), 6, 10);
            int chassisY = centerY + Mathf.RoundToInt(radius * 0.65f);

            DrawLine(pixels, width, height, leftX, chassisY, rightX, chassisY, thickness, GuideColor);
            DrawRing(pixels, width, height, leftX, centerY, radius, thickness, GuideColor);
            DrawRing(pixels, width, height, rightX, centerY, radius, thickness, GuideColor);

            int cabinBaseY = layout.MinY + Mathf.RoundToInt(drawingHeight * 0.22f);
            int cabinRoofY = layout.MinY + Mathf.RoundToInt(drawingHeight * 0.55f);
            int cabinLeft = Mathf.RoundToInt(Mathf.Lerp(layout.MinX, layout.MaxX, 0.29f));
            int roofLeft = Mathf.RoundToInt(Mathf.Lerp(layout.MinX, layout.MaxX, 0.45f));
            int roofRight = Mathf.RoundToInt(Mathf.Lerp(layout.MinX, layout.MaxX, 0.65f));
            int cabinRight = Mathf.RoundToInt(Mathf.Lerp(layout.MinX, layout.MaxX, 0.81f));
            DrawLine(pixels, width, height, cabinLeft, cabinBaseY, roofLeft, cabinRoofY, thickness, GuideColor);
            DrawLine(pixels, width, height, roofLeft, cabinRoofY, roofRight, cabinRoofY, thickness, GuideColor);
            DrawLine(pixels, width, height, roofRight, cabinRoofY, cabinRight, cabinBaseY, thickness, GuideColor);
        }

        private static bool TryFindInkBounds(Color32[] pixels, int width, int height,
            out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = width;
            minY = height;
            maxX = -1;
            maxY = -1;
            int inkCount = 0;

            for (int y = 0; y < height; y += 2)
            {
                for (int x = 0; x < width; x += 2)
                {
                    if (!IsInk(pixels[y * width + x])) continue;
                    inkCount++;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            return inkCount >= 40 && maxX >= minX && maxY >= minY;
        }

        private static bool HasLikelyWheelPair(Color32[] pixels, int textureWidth, int textureHeight,
            int minX, int minY, int maxX, int maxY)
        {
            int width = maxX - minX + 1;
            int radius = Mathf.Clamp(Mathf.RoundToInt(width * 0.12f), 16, 48);
            int centerY = Mathf.Clamp(minY + Mathf.RoundToInt(radius * 0.25f), radius,
                textureHeight - radius - 1);
            int leftX = Mathf.RoundToInt(Mathf.Lerp(minX, maxX, 0.25f));
            int rightX = Mathf.RoundToInt(Mathf.Lerp(minX, maxX, 0.75f));

            float leftDensity = InkDensity(pixels, textureWidth, textureHeight, leftX, centerY, radius);
            float rightDensity = InkDensity(pixels, textureWidth, textureHeight, rightX, centerY, radius);
            return leftDensity >= ExistingWheelDensity && rightDensity >= ExistingWheelDensity &&
                   Mathf.Max(leftDensity, rightDensity) >= StrongWheelDensity;
        }

        private static float InkDensity(Color32[] pixels, int width, int height, int centerX, int centerY, int radius)
        {
            int ink = 0;
            int total = 0;
            int radiusSquared = radius * radius;
            for (int y = -radius; y <= radius; y += 2)
            {
                for (int x = -radius; x <= radius; x += 2)
                {
                    if (x * x + y * y > radiusSquared) continue;
                    int px = Mathf.Clamp(centerX + x, 0, width - 1);
                    int py = Mathf.Clamp(centerY + y, 0, height - 1);
                    total++;
                    if (IsInk(pixels[py * width + px])) ink++;
                }
            }
            return total == 0 ? 0f : (float)ink / total;
        }

        private static bool IsInk(Color32 pixel)
        {
            return (pixel.r + pixel.g + pixel.b) / 3 < InkThreshold;
        }

        private static void DrawRing(Color32[] pixels, int width, int height, int centerX, int centerY,
            int radius, int thickness, Color32 color)
        {
            int outerSquared = radius * radius;
            int inner = Mathf.Max(1, radius - thickness);
            int innerSquared = inner * inner;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    int distanceSquared = x * x + y * y;
                    if (distanceSquared > outerSquared || distanceSquared < innerSquared) continue;
                    SetDarker(pixels, width, height, centerX + x, centerY + y, color);
                }
            }
        }

        private static void DrawLine(Color32[] pixels, int width, int height, int x0, int y0, int x1, int y1,
            int thickness, Color32 color)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            int radius = Mathf.Max(1, thickness / 2);
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : (float)i / steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int py = -radius; py <= radius; py++)
                for (int px = -radius; px <= radius; px++)
                    if (px * px + py * py <= radius * radius)
                        SetDarker(pixels, width, height, x + px, y + py, color);
            }
        }

        private static void SetDarker(Color32[] pixels, int width, int height, int x, int y, Color32 color)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            int index = y * width + x;
            Color32 current = pixels[index];
            pixels[index] = new Color32(Math.Min(current.r, color.r), Math.Min(current.g, color.g),
                Math.Min(current.b, color.b), current.a);
        }
    }
}
