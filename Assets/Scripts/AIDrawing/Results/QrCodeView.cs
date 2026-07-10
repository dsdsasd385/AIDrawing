using System;
using CarDrawing.Core;
using UnityEngine;

namespace CarDrawing.Results
{
    /// <summary>
    /// QR 코드 텍스처 생성기 (계획서 9-2). 결과 처리 시스템에 속하며,
    /// QrEncoder가 만든 모듈 행렬을 화면 표시용 Texture2D로 바꾼다.
    /// 표시는 ResultPanelController가 담당하고 여기서는 텍스처만 만든다.
    /// </summary>
    public static class QrCodeView
    {
        /// <summary>
        /// 문자열(다운로드 URL)을 QR 텍스처로 만든다.
        /// </summary>
        /// <param name="text">QR에 담을 문자열</param>
        /// <param name="moduleScale">모듈 1칸당 픽셀 수. 표시 크기보다 크게 잡아 확대 흐림을 피한다</param>
        /// <param name="quietZone">테두리 여백(모듈 단위). 사양 최소 4 — 줄이면 스캔율이 떨어진다</param>
        /// <returns>흑백 QR 텍스처. 인코딩 실패 시 null (호출부가 QR 영역을 숨기면 된다)</returns>
        public static Texture2D CreateTexture(string text, int moduleScale = 8, int quietZone = 4)
        {
            try
            {
                bool[,] modules = QrEncoder.Encode(text);
                int moduleCount = modules.GetLength(0);
                int sizePx = (moduleCount + quietZone * 2) * moduleScale;

                var pixels = new Color32[sizePx * sizePx];
                var white = new Color32(255, 255, 255, 255);
                var black = new Color32(0, 0, 0, 255);
                for (int i = 0; i < pixels.Length; i++) pixels[i] = white;

                for (int row = 0; row < moduleCount; row++)
                {
                    for (int col = 0; col < moduleCount; col++)
                    {
                        if (!modules[row, col]) continue;
                        // 텍스처 원점은 좌하단이므로 행을 뒤집어 행렬의 0행이 화면 위쪽에 오게 한다
                        int px = (quietZone + col) * moduleScale;
                        int py = sizePx - (quietZone + row + 1) * moduleScale;
                        for (int dy = 0; dy < moduleScale; dy++)
                            for (int dx = 0; dx < moduleScale; dx++)
                                pixels[(py + dy) * sizePx + px + dx] = black;
                    }
                }

                var texture = new Texture2D(sizePx, sizePx, TextureFormat.RGBA32, false);
                texture.SetPixels32(pixels);
                texture.Apply();
                // 모듈 경계가 뭉개지면 스캔이 안 되므로 보간 없이 표시
                texture.filterMode = FilterMode.Point;
                return texture;
            }
            catch (Exception e)
            {
                // QR 실패는 부가 기능 실패 — 체험은 계속돼야 한다 (계획서 3장 핵심 원칙)
                LogManager.Warn($"[QR] 생성 실패: {e.Message}");
                return null;
            }
        }
    }
}
