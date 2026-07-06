using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EarthCoding.Data;

namespace EarthCoding.Blocks
{
    /// <summary>
    /// 왼쪽 '블록 선택 영역'에 표시되는 블록 견본.
    /// 드래그해서 가운데 조립 영역에 놓으면 새 BlockInstance 가 만들어진다.
    /// 팔레트의 견본 자체는 사라지지 않으므로 같은 블록을 여러 번 사용할 수 있다.
    /// BlockPalette(에피소드 시작 시)가 JSON 의 BlockEntry 목록으로 생성한다.
    /// </summary>
    public class PaletteBlockView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>이 견본이 나타내는 블록 정의 (JSON)</summary>
        private BlockEntry _entry;

        /// <summary>드롭 대상 조립 패널</summary>
        private BlockAssemblyPanel _panel;

        /// <summary>
        /// 팔레트 견본 화면을 구성한다.
        /// </summary>
        /// <param name="entry">블록 정의</param>
        /// <param name="panel">드롭 대상 조립 패널</param>
        public void Setup(BlockEntry entry, BlockAssemblyPanel panel)
        {
            _entry = entry;
            _panel = panel;

            // 팔레트 목록 안에서 차지할 높이 (이름 + 설명 2줄)
            var layout = gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 78;

            // 블록 종류별 색상 배경
            var bg = gameObject.AddComponent<Image>();
            bg.color = BlockStyle.GetColor(entry.Type);

            // 모양 기호 배지 (형태로 종류 구분)
            var badge = UI.UIFactory.Panel(transform, "ShapeBadge",
                new Vector2(0, 0), new Vector2(0.14f, 1), new Color(0, 0, 0, 0.25f));
            badge.raycastTarget = false;
            var symbol = UI.UIFactory.Label(badge.transform, "Symbol", BlockStyle.GetShapeSymbol(entry.Type), 24);
            symbol.raycastTarget = false;

            // 블록 이름 (위쪽)
            var name = UI.UIFactory.Label(transform, "Name", entry.Name, 21, TextAnchor.MiddleLeft);
            var nameRt = (RectTransform)name.transform;
            nameRt.anchorMin = new Vector2(0.17f, 0.5f);
            nameRt.anchorMax = new Vector2(0.98f, 1f);
            name.raycastTarget = false;

            // 블록 설명 (아래쪽, 작은 글씨) - JSON 의 Description
            var desc = UI.UIFactory.Label(transform, "Desc", entry.Description, 14, TextAnchor.UpperLeft);
            var descRt = (RectTransform)desc.transform;
            descRt.anchorMin = new Vector2(0.17f, 0.05f);
            descRt.anchorMax = new Vector2(0.98f, 0.5f);
            desc.color = new Color(1f, 1f, 1f, 0.85f);
            desc.raycastTarget = false;
        }

        /// <summary>드래그 시작: 잔상을 띄운다. 견본은 그대로 남는다.</summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            DragGhost.Show(_panel.RootCanvas, _entry.Name, BlockStyle.GetColor(_entry.Type));
            DragGhost.Move(eventData.position, _panel.RootCanvas);
        }

        /// <summary>드래그 중: 잔상 이동 + 조립 영역 삽입 위치 표시</summary>
        public void OnDrag(PointerEventData eventData)
        {
            DragGhost.Move(eventData.position, _panel.RootCanvas);
            _panel.UpdateInsertIndicator(eventData.position);
        }

        /// <summary>
        /// 드래그 종료: 조립 영역 위에서 놓으면 새 블록을 만들어 스냅시킨다.
        /// </summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            DragGhost.Hide();
            _panel.HideInsertIndicator();

            if (_panel.IsPointerOver(eventData.position))
            {
                // 조립 영역에 새 블록 추가 (놓은 위치에 자동 스냅)
                _panel.AddNewBlock(_entry, eventData.position);
            }
            // 조립 영역 밖에서 놓으면 아무 일도 일어나지 않는다 (취소)
        }
    }
}
