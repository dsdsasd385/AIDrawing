using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EarthCoding.Blocks
{
    /// <summary>
    /// 조립 영역에 놓인 블록 1개의 화면 표현.
    /// 드래그로 순서를 바꾸거나(위치 교체), 조립 영역 밖으로 끌어내 삭제할 수 있다.
    /// 시작/종료 블록은 IsFixed=true 로 드래그·삭제가 불가능하다. (작업계획서 5장)
    /// BlockAssemblyPanel 이 생성/관리한다.
    /// </summary>
    public class BlockView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>이 화면 블록이 나타내는 런타임 블록 데이터</summary>
        public BlockInstance Instance { get; private set; }

        /// <summary>시작/종료 블록 여부. true 면 드래그와 삭제가 막힌다.</summary>
        public bool IsFixed { get; private set; }

        /// <summary>소속 조립 패널 (드래그 결과를 통지할 대상)</summary>
        private BlockAssemblyPanel _panel;

        /// <summary>블록 배경 이미지 (드래그 중 반투명 처리용)</summary>
        private Image _background;

        /// <summary>
        /// 에피소드가 드롭다운 등 추가 UI를 붙일 수 있는 블록 오른쪽 영역.
        /// 예) Episode1 의 늘리기/줄이기, Episode4 의 반복 횟수
        /// </summary>
        public RectTransform OptionArea { get; private set; }

        /// <summary>
        /// 블록 화면을 구성한다. BlockAssemblyPanel.CreateView 에서 호출한다.
        /// </summary>
        /// <param name="instance">표시할 블록 데이터 (시작/종료는 null 가능)</param>
        /// <param name="panel">소속 조립 패널</param>
        /// <param name="displayName">표시 이름</param>
        /// <param name="type">블록 종류 (색/기호 결정)</param>
        /// <param name="isFixed">시작/종료 고정 블록 여부</param>
        public void Setup(BlockInstance instance, BlockAssemblyPanel panel,
            string displayName, string type, bool isFixed)
        {
            Instance = instance;
            _panel = panel;
            IsFixed = isFixed;

            // 세로 목록 안에서 일정 높이를 차지하도록 설정
            var layout = gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 56;

            // 블록 배경 (종류별 색상)
            _background = gameObject.AddComponent<Image>();
            _background.color = BlockStyle.GetColor(type);

            // 왼쪽 모양 기호 배지: 색을 몰라도 형태로 종류를 구분 (작업계획서 5장)
            var badge = UI.UIFactory.Panel(transform, "ShapeBadge",
                new Vector2(0, 0), new Vector2(0.13f, 1), new Color(0, 0, 0, 0.25f));
            badge.raycastTarget = false;
            var symbol = UI.UIFactory.Label(badge.transform, "Symbol", BlockStyle.GetShapeSymbol(type), 24);
            symbol.raycastTarget = false;

            // 블록 이름
            var label = UI.UIFactory.Label(transform, "Name", displayName, 21, TextAnchor.MiddleLeft);
            var labelRt = (RectTransform)label.transform;
            labelRt.anchorMin = new Vector2(0.15f, 0);
            labelRt.anchorMax = new Vector2(0.55f, 1);
            label.raycastTarget = false;

            // 에피소드별 추가 UI(드롭다운 등)가 붙는 영역
            OptionArea = UI.UIFactory.Rect(transform, "OptionArea", new Vector2(0.55f, 0.08f), new Vector2(0.98f, 0.92f));
        }

        /// <summary>
        /// 드래그 시작: 원본을 반투명하게 하고 잔상을 띄운다. 고정 블록은 무시.
        /// </summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (IsFixed) return;
            _background.color = new Color(_background.color.r, _background.color.g, _background.color.b, 0.35f);
            DragGhost.Show(_panel.RootCanvas, Instance.Entry.Name, BlockStyle.GetColor(Instance.Entry.Type));
            DragGhost.Move(eventData.position, _panel.RootCanvas);
        }

        /// <summary>드래그 중: 잔상을 포인터에 붙이고 삽입 위치 표시를 갱신한다.</summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (IsFixed) return;
            DragGhost.Move(eventData.position, _panel.RootCanvas);
            _panel.UpdateInsertIndicator(eventData.position);
        }

        /// <summary>
        /// 드래그 종료: 조립 영역 위에서 놓으면 순서 이동, 밖에서 놓으면 삭제한다.
        /// </summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (IsFixed) return;
            DragGhost.Hide();
            _panel.HideInsertIndicator();

            // 원본 투명도 복원
            _background.color = BlockStyle.GetColor(Instance.Entry.Type);

            if (_panel.IsPointerOver(eventData.position))
            {
                // 조립 영역 안: 새 위치로 이동 (자동 스냅)
                _panel.MoveBlock(this, eventData.position);
            }
            else
            {
                // 조립 영역 밖: 블록 삭제
                _panel.RemoveBlock(this);
            }
        }
    }
}
