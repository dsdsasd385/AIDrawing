using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EarthCoding.Data;

namespace EarthCoding.Blocks
{
    /// <summary>
    /// 가운데 '블록 조립 영역' 관리자. (작업계획서 4~5장, 7장 Drag&Drop 대응)
    /// [시작] ~ [종료] 사이에 명령 블록들을 세로로 배치하고
    /// 추가/이동/삭제/초기화와 자동 스냅(삽입 위치 계산)을 담당한다.
    /// 체험자가 만든 프로그램은 GetProgram() 으로 에피소드 로직에 전달된다.
    /// </summary>
    public class BlockAssemblyPanel : MonoBehaviour
    {
        /// <summary>최상단 캔버스 (드래그 잔상 좌표 기준)</summary>
        public Canvas RootCanvas { get; private set; }

        /// <summary>조립 영역에 놓을 수 있는 명령 블록 최대 개수 (화면 공간 보호)</summary>
        public int MaxBlocks = 8;

        /// <summary>블록이 추가/이동/삭제될 때 통지 (에피소드가 상태 갱신에 사용)</summary>
        public event Action OnProgramChanged;

        /// <summary>새 블록 화면이 만들어질 때 통지 (에피소드가 드롭다운 등을 붙임)</summary>
        public event Action<BlockView> OnBlockCreated;

        /// <summary>블록들이 쌓이는 세로 목록 콘텐츠</summary>
        private RectTransform _content;

        /// <summary>드롭 판정에 사용하는 조립 영역 전체 사각형</summary>
        private RectTransform _dropArea;

        /// <summary>현재 조립된 명령 블록 화면 목록 (시작/종료 제외, 순서 = 실행 순서)</summary>
        private readonly List<BlockView> _items = new List<BlockView>();

        /// <summary>고정 시작 블록 화면</summary>
        private BlockView _startView;

        /// <summary>고정 종료 블록 화면</summary>
        private BlockView _endView;

        /// <summary>삽입 위치를 표시하는 밝은 가로선</summary>
        private Image _indicator;

        /// <summary>
        /// 조립 패널을 초기화한다. 에피소드 화면 구성 시 1회 호출.
        /// </summary>
        /// <param name="canvas">최상단 캔버스</param>
        /// <param name="dropArea">조립 영역 배경 (드롭 판정 사각형)</param>
        /// <param name="content">블록이 쌓일 세로 레이아웃 콘텐츠</param>
        public void Initialize(Canvas canvas, RectTransform dropArea, RectTransform content)
        {
            RootCanvas = canvas;
            _dropArea = dropArea;
            _content = content;

            // 시작/종료 고정 블록 생성 (삭제 불가능 - 작업계획서 5장)
            _startView = CreateView(null, "시작", "Start", true);
            _endView = CreateView(null, "종료", "End", true);

            // 삽입 위치 표시선 (평소 숨김)
            _indicator = UI.UIFactory.Panel(_content, "InsertIndicator",
                Vector2.zero, Vector2.zero, new Color(1f, 0.95f, 0.4f, 0.9f));
            _indicator.raycastTarget = false;
            var indLayout = _indicator.gameObject.AddComponent<LayoutElement>();
            indLayout.preferredHeight = 6;
            _indicator.gameObject.SetActive(false);
        }

        /// <summary>
        /// 팔레트에서 끌어온 블록 정의로 새 블록을 만들어 놓은 위치에 삽입한다.
        /// </summary>
        /// <param name="entry">블록 정의 (JSON)</param>
        /// <param name="screenPos">드롭한 화면 좌표 (삽입 위치 계산용)</param>
        /// <returns>생성된 블록 화면 (가득 차면 null)</returns>
        public BlockView AddNewBlock(BlockEntry entry, Vector2 screenPos)
        {
            // 최대 개수 초과 시 추가하지 않는다 (화면 밖으로 넘치는 것 방지)
            if (_items.Count >= MaxBlocks)
            {
                Core.LogManager.Write("Info", "블록 최대 개수 초과로 추가 거부");
                return null;
            }

            var instance = new BlockInstance(entry);
            var view = CreateView(instance, entry.Name, entry.Type, false);

            int index = GetInsertIndex(screenPos);
            _items.Insert(index, view);
            ApplyOrder();

            OnBlockCreated?.Invoke(view);
            OnProgramChanged?.Invoke();
            return view;
        }

        /// <summary>
        /// 이미 놓인 블록을 드롭 위치에 맞춰 순서 이동한다. (위치 교체)
        /// </summary>
        /// <param name="view">이동할 블록 화면</param>
        /// <param name="screenPos">드롭한 화면 좌표</param>
        public void MoveBlock(BlockView view, Vector2 screenPos)
        {
            if (!_items.Contains(view)) return;

            // 자기 자신을 목록에서 잠시 빼고 새 위치를 계산해야 인덱스가 어긋나지 않는다
            _items.Remove(view);
            int index = GetInsertIndex(screenPos);
            _items.Insert(Mathf.Clamp(index, 0, _items.Count), view);

            ApplyOrder();
            OnProgramChanged?.Invoke();
        }

        /// <summary>
        /// 블록을 조립 영역에서 삭제한다. (조립 영역 밖으로 드래그 시 호출)
        /// </summary>
        /// <param name="view">삭제할 블록 화면</param>
        public void RemoveBlock(BlockView view)
        {
            if (view.IsFixed) return;   // 시작/종료는 삭제 불가
            _items.Remove(view);
            Destroy(view.gameObject);
            OnProgramChanged?.Invoke();
        }

        /// <summary>
        /// 모든 명령 블록을 제거한다. 하단 '초기화' 버튼과 에피소드 전환 시 호출.
        /// </summary>
        public void Clear()
        {
            foreach (var v in _items)
                Destroy(v.gameObject);
            _items.Clear();
            OnProgramChanged?.Invoke();
        }

        /// <summary>
        /// 체험자가 조립한 프로그램(블록 순서 목록)을 반환한다.
        /// 실행 시스템이 이 목록을 순차 실행한다.
        /// </summary>
        public List<BlockInstance> GetProgram()
        {
            var program = new List<BlockInstance>();
            foreach (var v in _items)
                program.Add(v.Instance);
            return program;
        }

        /// <summary>현재 조립된 명령 블록 개수</summary>
        public int BlockCount => _items.Count;

        /// <summary>
        /// 화면 좌표가 조립 영역 안인지 판정한다. (드롭 가능 여부)
        /// </summary>
        /// <param name="screenPos">화면 좌표</param>
        public bool IsPointerOver(Vector2 screenPos)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(
                _dropArea, screenPos, RootCanvas.worldCamera);
        }

        /// <summary>
        /// 드래그 중 삽입 위치 표시선을 갱신한다. 조립 영역 밖이면 숨긴다.
        /// </summary>
        /// <param name="screenPos">현재 포인터 화면 좌표</param>
        public void UpdateInsertIndicator(Vector2 screenPos)
        {
            if (!IsPointerOver(screenPos))
            {
                HideInsertIndicator();
                return;
            }

            _indicator.gameObject.SetActive(true);
            // 표시선을 삽입될 위치(시작 블록 다음 + index)로 이동시킨다
            int index = GetInsertIndex(screenPos);
            _indicator.transform.SetSiblingIndex(_startView.transform.GetSiblingIndex() + 1 + index);
        }

        /// <summary>삽입 위치 표시선을 숨긴다.</summary>
        public void HideInsertIndicator()
        {
            if (_indicator != null) _indicator.gameObject.SetActive(false);
        }

        /// <summary>
        /// 화면 좌표로부터 블록이 삽입될 인덱스를 계산한다. (자동 스냅의 핵심)
        /// 각 블록의 화면상 중심 Y와 비교하여, 포인터보다 위에 있는 블록 수 = 삽입 인덱스.
        /// </summary>
        /// <param name="screenPos">포인터 화면 좌표</param>
        /// <returns>0 ~ 블록 수 사이의 삽입 인덱스</returns>
        private int GetInsertIndex(Vector2 screenPos)
        {
            int index = 0;
            foreach (var item in _items)
            {
                // 블록 중심의 화면 Y 좌표 (Overlay 캔버스이므로 카메라 null)
                var centerY = RectTransformUtility.WorldToScreenPoint(
                    RootCanvas.worldCamera, item.transform.position).y;
                // 포인터가 블록 중심보다 아래이면 그 블록 뒤에 삽입
                if (screenPos.y < centerY) index++;
            }
            return index;
        }

        /// <summary>
        /// 블록 화면 오브젝트를 생성한다.
        /// </summary>
        /// <param name="instance">블록 데이터 (고정 블록은 null)</param>
        /// <param name="displayName">표시 이름</param>
        /// <param name="type">블록 종류</param>
        /// <param name="isFixed">시작/종료 고정 여부</param>
        private BlockView CreateView(BlockInstance instance, string displayName, string type, bool isFixed)
        {
            var go = new GameObject($"Block_{displayName}", typeof(RectTransform));
            go.transform.SetParent(_content, false);
            var view = go.AddComponent<BlockView>();
            view.Setup(instance, this, displayName, type, isFixed);
            return view;
        }

        /// <summary>
        /// 목록 순서를 화면 계층 순서에 반영한다.
        /// 시작 → 명령 블록들 → 종료 순으로 SiblingIndex 를 재배치한다.
        /// </summary>
        private void ApplyOrder()
        {
            _startView.transform.SetSiblingIndex(0);
            for (int i = 0; i < _items.Count; i++)
                _items[i].transform.SetSiblingIndex(i + 1);
            _endView.transform.SetSiblingIndex(_items.Count + 1);
        }
    }
}
