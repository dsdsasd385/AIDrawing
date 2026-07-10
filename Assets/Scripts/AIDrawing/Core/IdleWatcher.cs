using UnityEngine;

namespace CarDrawing.Core
{
    /// <summary>
    /// 무입력(방치) 시간 측정기. 코어 시스템에 속하며 AppFlowManager가
    /// 상태별 방치 정책(계획서 4장: 그리기 90+30초, 스타일/결과 자동 복귀)을 판단할 때 읽는다.
    /// 마우스 이동·클릭·키 입력이 있으면 0으로 돌아간다.
    /// </summary>
    public class IdleWatcher : MonoBehaviour
    {
        /// <summary>마지막 입력 이후 경과 시간(초)</summary>
        public float IdleSeconds { get; private set; }

        private Vector3 _lastMousePosition;

        private void Update()
        {
            // anyKey는 마우스 버튼도 포함한다. 이동은 미세 떨림을 무시하도록 2픽셀 이상만 인정
            bool moved = (Input.mousePosition - _lastMousePosition).sqrMagnitude > 4f;
            if (Input.anyKey || moved)
                IdleSeconds = 0f;
            else
                IdleSeconds += Time.unscaledDeltaTime;

            _lastMousePosition = Input.mousePosition;
        }

        /// <summary>상태 전환 등에서 방치 시간을 강제로 초기화한다.</summary>
        public void ResetIdle() => IdleSeconds = 0f;
    }
}
