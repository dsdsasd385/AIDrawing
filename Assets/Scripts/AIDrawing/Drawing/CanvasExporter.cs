using UnityEngine;

namespace CarDrawing.Drawing
{
    /// <summary>
    /// RenderTexture를 CPU 텍스처/PNG로 변환하는 정적 헬퍼.
    /// 드로잉 시스템에 속하며, DrawingCanvas(undo 스냅샷)와
    /// 생성 파이프라인(ComfyUIClient에 보낼 PNG 인코딩)이 함께 사용한다.
    /// </summary>
    public static class CanvasExporter
    {
        /// <summary>
        /// RenderTexture 내용을 새 Texture2D로 복사한다.
        /// </summary>
        /// <param name="source">복사할 RenderTexture</param>
        /// <returns>호출자가 파괴 책임을 갖는 새 Texture2D</returns>
        public static Texture2D ToTexture2D(RenderTexture source)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = source;

            var tex = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            tex.Apply();

            RenderTexture.active = previous;
            return tex;
        }

        /// <summary>
        /// RenderTexture 내용을 PNG 바이트로 인코딩한다 (ComfyUI 업로드용).
        /// </summary>
        /// <param name="source">인코딩할 RenderTexture</param>
        /// <returns>PNG 파일 바이트</returns>
        public static byte[] ToPng(RenderTexture source)
        {
            Texture2D tex = ToTexture2D(source);
            byte[] png = tex.EncodeToPNG();
            Object.Destroy(tex);
            return png;
        }
    }
}
