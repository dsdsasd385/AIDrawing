using UnityEngine;

/// <summary>
/// 프로젝트 전역 단축키를 한 곳에서 관리하는 매니저.
/// 패키지 import만 하면 자동 생성되어 동작한다(씬 배치 불필요).
/// 키 추가/변경은 이 스크립트에서만 수정한다.
/// </summary>
public class HotkeyManager : MonoBehaviour
{
    [Header("단축키 (Ctrl+Shift+ 조합)")]
    [SerializeField] private KeyCode toggleReporterKey = KeyCode.D;  // Ctrl+Shift+D : 로그 뷰어 토글
    [SerializeField] private KeyCode toggleCursorKey   = KeyCode.M;  // Ctrl+Shift+M : 커서 표시 토글

    private static HotkeyManager _instance;
    private Reporter _reporter;

    // 패키지만 넣으면 씬 세팅 없이 자동 설치 (InstallTimeStampLogger와 동일한 패턴)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInstall()
    {
        if (_instance != null) return;
        if (FindObjectOfType<HotkeyManager>() != null) return; // 씬에 수동 배치돼 있으면 그걸 사용
        new GameObject("[HotkeyManager]").AddComponent<HotkeyManager>();
    }

    private void Awake()
    {
        // 싱글톤 - 중복 인스턴스 제거
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 플레이 진입 시 Reporter는 항상 숨김 상태로 시작 (PlayerPrefs 복원값 무시)
        if (_reporter == null) _reporter = FindObjectOfType<Reporter>();
        if (_reporter != null) _reporter.SetShow(false);
    }

    private void Update()
    {
        if (Chord(toggleReporterKey)) ToggleReporter();
        if (Chord(toggleCursorKey))   Cursor.visible = !Cursor.visible;
    }

    // Ctrl + Shift + key 조합 판정 (좌우 Ctrl/Shift 모두 허용)
    private bool Chord(KeyCode key)
    {
        bool ctrl  = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift)   || Input.GetKey(KeyCode.RightShift);
        return ctrl && shift && Input.GetKeyDown(key);
    }

    private void ToggleReporter()
    {
        // Reporter가 씬에 없을 수 있으니 캐시 후 필요 시 재탐색
        if (_reporter == null) _reporter = FindObjectOfType<Reporter>();
        if (_reporter != null) _reporter.ToggleShow();
    }
}
