using System.Collections.Generic;
using UnityEngine;

namespace CarDrawing.Drawing
{
    /// <summary>
    /// 이중 레이어 그리기 캔버스의 핵심.
    /// - LineLayer: 검정 선만 기록 (ControlNet Scribble 입력용)
    /// - ColorLayer: 색 포함 전체 그림 (img2img 초기 이미지용)
    /// 드로잉 시스템에 속하며, CanvasMouseInput이 좌표를 넣어주고
    /// DrawingPanel(UI)이 도구 상태를 설정한다. 내보내기는 CanvasExporter를 사용한다.
    /// </summary>
    public class DrawingCanvas : MonoBehaviour
    {
        /// <summary>캔버스 가로 해상도. 계획서 6장: SD 1.5 안정 범위인 768×512 고정</summary>
        public const int Width = 768;
        /// <summary>캔버스 세로 해상도</summary>
        public const int Height = 512;

        /// <summary>undo 스냅샷 최대 보관 수. 초과 시 가장 오래된 것부터 버린다 (메모리 제한)</summary>
        [SerializeField] private int maxUndoSteps = 10;

        /// <summary>선 레이어 (검정 선화). ControlNet Scribble에 그대로 들어간다</summary>
        public RenderTexture LineLayer { get; private set; }
        /// <summary>색 레이어 (관람객이 보는 그림). img2img 초기 이미지가 된다</summary>
        public RenderTexture ColorLayer { get; private set; }

        /// <summary>현재 브러시 색 (색 레이어에만 적용, 선 레이어는 항상 검정)</summary>
        public Color BrushColor { get; set; } = Color.black;
        /// <summary>현재 브러시 반지름(픽셀). 펜 굵기 3단계는 UI에서 이 값으로 매핑한다</summary>
        public float BrushRadius { get; set; } = 6f;
        /// <summary>지우개 모드 여부. 지우개는 양쪽 레이어에 흰색을 칠한다</summary>
        public bool IsEraser { get; set; }

        // GL 즉시 모드 드로잉용 머티리얼 (버텍스 컬러를 그대로 출력하는 내장 셰이더)
        private Material _drawMaterial;

        // undo 스냅샷 스택. 스트로크 시작 시점의 (선, 색) 텍스처 쌍을 저장한다
        private readonly List<(Texture2D line, Texture2D color)> _undoStack = new List<(Texture2D, Texture2D)>();

        // 스트로크 진행 중 직전 좌표 (픽셀 단위). 세그먼트 보간에 사용
        private Vector2 _lastPixel;
        private bool _strokeActive;

        private void Awake()
        {
            LineLayer = CreateLayer();
            ColorLayer = CreateLayer();

            // Internal-Colored: 조명 없이 버텍스 컬러만 출력하는 에디터/런타임 공용 셰이더
            _drawMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            _drawMaterial.hideFlags = HideFlags.HideAndDontSave;

            ClearAll();
        }

        private void OnDestroy()
        {
            if (LineLayer != null) LineLayer.Release();
            if (ColorLayer != null) ColorLayer.Release();
            ClearUndoStack();
        }

        private static RenderTexture CreateLayer()
        {
            var rt = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear
            };
            rt.Create();
            return rt;
        }

        /// <summary>
        /// 스트로크 시작. undo 스냅샷을 이 시점에 저장한다 (스트로크 단위 undo).
        /// </summary>
        /// <param name="uv">캔버스 UV 좌표 (0~1, 좌하단 원점)</param>
        public void BeginStroke(Vector2 uv)
        {
            PushUndoSnapshot();
            _strokeActive = true;
            _lastPixel = UvToPixel(uv);
            StampSegment(_lastPixel, _lastPixel);
        }

        /// <summary>
        /// 스트로크 진행. 직전 좌표와 현재 좌표 사이를 이어 그린다.
        /// </summary>
        /// <param name="uv">캔버스 UV 좌표 (0~1)</param>
        public void ContinueStroke(Vector2 uv)
        {
            if (!_strokeActive) return;
            Vector2 pixel = UvToPixel(uv);
            StampSegment(_lastPixel, pixel);
            _lastPixel = pixel;
        }

        /// <summary>스트로크 종료.</summary>
        public void EndStroke()
        {
            _strokeActive = false;
        }

        /// <summary>마지막 스트로크를 취소하고 그 이전 상태로 되돌린다.</summary>
        public void Undo()
        {
            if (_undoStack.Count == 0) return;

            var (line, color) = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            Graphics.Blit(line, LineLayer);
            Graphics.Blit(color, ColorLayer);
            Destroy(line);
            Destroy(color);
        }

        /// <summary>양쪽 레이어를 흰색으로 초기화하고 undo 기록도 비운다 (새 관람객 시작 시).</summary>
        public void ClearAll()
        {
            FillLayer(LineLayer, Color.white);
            FillLayer(ColorLayer, Color.white);
            ClearUndoStack();
            _strokeActive = false;
        }

        /// <summary>그린 내용이 하나라도 있는지 (undo 기록 유무로 판단). 빈 그림 제출 방지용</summary>
        public bool HasStrokes => _undoStack.Count > 0;

        private static Vector2 UvToPixel(Vector2 uv)
        {
            return new Vector2(Mathf.Clamp01(uv.x) * Width, Mathf.Clamp01(uv.y) * Height);
        }

        // 두 점 사이를 두꺼운 선(사각형) + 양 끝 원으로 채워 자연스러운 스트로크를 만든다
        private void StampSegment(Vector2 from, Vector2 to)
        {
            // 선 레이어: 지우개면 흰색, 아니면 항상 검정 (색과 무관하게 형태만 기록)
            Color lineColor = IsEraser ? Color.white : Color.black;
            // 색 레이어: 지우개면 흰색, 아니면 선택한 브러시 색
            Color colorColor = IsEraser ? Color.white : BrushColor;
            // 지우개는 흔적이 남지 않도록 브러시보다 넉넉하게 지운다
            float radius = IsEraser ? BrushRadius * 2.5f : BrushRadius;

            DrawSegmentOn(LineLayer, from, to, radius, lineColor);
            DrawSegmentOn(ColorLayer, from, to, radius, colorColor);
        }

        private void DrawSegmentOn(RenderTexture target, Vector2 from, Vector2 to, float radius, Color color)
        {
            RenderTexture.active = target;
            GL.PushMatrix();
            // 픽셀 좌표계 (좌하단 원점, UV와 동일 방향)
            GL.LoadPixelMatrix(0, Width, 0, Height);
            _drawMaterial.SetPass(0);

            // 몸통: 선분에 수직인 방향으로 radius만큼 확장한 사각형
            Vector2 dir = to - from;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Vector2 normal = new Vector2(-dir.y, dir.x).normalized * radius;
                GL.Begin(GL.QUADS);
                GL.Color(color);
                GL.Vertex3(from.x + normal.x, from.y + normal.y, 0);
                GL.Vertex3(from.x - normal.x, from.y - normal.y, 0);
                GL.Vertex3(to.x - normal.x, to.y - normal.y, 0);
                GL.Vertex3(to.x + normal.x, to.y + normal.y, 0);
                GL.End();
            }

            // 양 끝 캡: 원 (삼각형 팬). 캡이 없으면 꺾이는 지점에 각이 진다
            DrawDisc(from, radius, color);
            DrawDisc(to, radius, color);

            GL.PopMatrix();
            RenderTexture.active = null;
        }

        private void DrawDisc(Vector2 center, float radius, Color color)
        {
            const int segments = 24;
            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 2f * i / segments;
                float a1 = Mathf.PI * 2f * (i + 1) / segments;
                GL.Vertex3(center.x, center.y, 0);
                GL.Vertex3(center.x + Mathf.Cos(a0) * radius, center.y + Mathf.Sin(a0) * radius, 0);
                GL.Vertex3(center.x + Mathf.Cos(a1) * radius, center.y + Mathf.Sin(a1) * radius, 0);
            }
            GL.End();
        }

        private void FillLayer(RenderTexture target, Color color)
        {
            RenderTexture.active = target;
            GL.Clear(true, true, color);
            RenderTexture.active = null;
        }

        private void PushUndoSnapshot()
        {
            // 스냅샷은 CPU 텍스처로 보관 (RenderTexture를 N개 유지하는 것보다 관리가 단순)
            _undoStack.Add((CanvasExporter.ToTexture2D(LineLayer), CanvasExporter.ToTexture2D(ColorLayer)));

            // 오래된 스냅샷부터 폐기하여 메모리 상한 유지
            while (_undoStack.Count > maxUndoSteps)
            {
                Destroy(_undoStack[0].line);
                Destroy(_undoStack[0].color);
                _undoStack.RemoveAt(0);
            }
        }

        private void ClearUndoStack()
        {
            foreach (var (line, color) in _undoStack)
            {
                Destroy(line);
                Destroy(color);
            }
            _undoStack.Clear();
        }
    }
}
