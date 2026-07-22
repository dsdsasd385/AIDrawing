using System;
using System.Collections.Generic;
using System.IO;
using CarDrawing.Core;
using CarDrawing.Generation;
using CarDrawing.Results;
using UnityEngine;
using UnityEngine.UI;

namespace CarDrawing.UI
{
    /// <summary>
    /// 관리자 모드(AdminPanel). 계획서 11장: 숨김 키 조합(Ctrl+Shift+F12) + 비밀번호로 진입.
    /// UI 시스템에 속하며 AppFlowManager가 키 조합을 감지해 이 패널을 띄운다.
    /// 기능: 상태 확인(ComfyUI·업로더·필터·버전) / 테스트 생성 / 서버 재시작 /
    ///       갤러리 작품 삭제 · 격리 작품 복원 / 설정 JSON 다시 읽기 / 폴더 열기 / 갤러리 초기화.
    /// 관람객이 실수로 들어와도 비밀번호에서 막히고, 어떤 동작이 실패해도 앱은 계속 산다.
    /// </summary>
    public class AdminPanelController : MonoBehaviour
    {
        /// <summary>[닫기]를 눌렀을 때 (AppFlowManager가 대기 화면으로 되돌린다)</summary>
        public event Action CloseRequested;

        // 목록 그리드 (한 페이지에 보여줄 작품 수). 썸네일 4열 × 2행
        private const int Columns = 4;
        private const int Rows = 2;
        private const int PageSize = Columns * Rows;

        private ComfyUIClient _comfyClient;
        private ComfyUIWatchdog _watchdog;
        private B2Uploader _b2;
        private GcsUploader _gcs;

        // 잠금(PIN) 화면과 본문
        private GameObject _lockGroup;
        private Text _pinDisplay;   // 입력한 자릿수만큼 ●
        private string _pin = "";   // 온스크린 키패드로 입력 중인 PIN
        private Text _lockMessage;
        private GameObject _bodyGroup;

        private Text _statusText;   // 좌측 상태 블록
        private Text _messageText;  // 하단 최근 동작 결과
        private Text _listTitle;
        private Text _pageLabel;

        // 목록 상태
        private bool _showQuarantine;   // false=갤러리, true=격리
        private int _page;
        private readonly List<GameObject> _cells = new List<GameObject>();
        private readonly List<Texture2D> _thumbnails = new List<Texture2D>();
        private Transform _grid;

        // 갤러리 초기화는 두 번 눌러야 실행된다 (오조작 방지)
        private bool _wipeArmed;
        private Text _wipeLabel;

        private void Awake()
        {
            // 씬에 이전 런타임 UI가 남아 있으면 중복 생성된다 (ResultPanel과 같은 방어)
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            UiBuilder.Stretch((RectTransform)transform);
            Image background = UiBuilder.CreateImage(transform, "Background", new Color(0.08f, 0.09f, 0.13f));
            UiBuilder.Stretch((RectTransform)background.transform);

            BuildLockScreen(background.transform);
            BuildBody(background.transform);
        }

        private void OnEnable()
        {
            ResolveReferences();

            // 진입할 때마다 다시 잠근다 — 한 번 열었다고 다음 진입이 무방비가 되면 안 된다
            bool needPassword = !string.IsNullOrEmpty(ConfigManager.Config.admin.password);
            _lockGroup.SetActive(needPassword);
            _bodyGroup.SetActive(!needPassword);
            _pin = "";
            ApplyPinDisplay();

            _wipeArmed = false;
            _page = 0;
            SetMessage("");
            if (!needPassword) RefreshAll();
        }

        private void OnDisable()
        {
            ClearThumbnails(); // 장시간 무인 운영 — 관리자 화면을 여닫아도 텍스처가 쌓이지 않게
        }

        private void ResolveReferences()
        {
            if (_comfyClient == null) _comfyClient = FindObjectOfType<ComfyUIClient>(true);
            if (_watchdog == null) _watchdog = FindObjectOfType<ComfyUIWatchdog>(true);
            if (_b2 == null) _b2 = FindObjectOfType<B2Uploader>(true);
            if (_gcs == null) _gcs = FindObjectOfType<GcsUploader>(true);
        }

        // ── 화면 구성 ────────────────────────────────────────

        // 잠금 화면. 전시 키오스크에는 키보드가 없으므로 입력은 **온스크린 숫자 키패드**로 받는다
        // (텍스트 InputField는 터치만으로는 아무것도 못 친다 — 2026-07-14 플레이 확인).
        // 그래서 비밀번호는 숫자 PIN으로 운영한다 (Config `admin.password`)
        private void BuildLockScreen(Transform parent)
        {
            Image panel = UiBuilder.CreateImage(parent, "LockGroup", new Color(0.12f, 0.13f, 0.18f));
            UiBuilder.Place((RectTransform)panel.transform, Vector2.zero, new Vector2(620, 860));
            _lockGroup = panel.gameObject;

            Text title = UiBuilder.CreateText(panel.transform, "LockTitle",
                TextLibrary.Get("admin.title"), 44, Color.white);
            UiBuilder.Place((RectTransform)title.transform, new Vector2(0, 360), new Vector2(560, 60));

            // 입력 표시 (자릿수만큼 ● — 어깨너머로 안 보이게)
            Image field = UiBuilder.CreateImage(panel.transform, "PinDisplay", new Color(0.95f, 0.96f, 0.99f));
            UiBuilder.Place((RectTransform)field.transform, new Vector2(0, 270), new Vector2(480, 76));
            _pinDisplay = UiBuilder.CreateText(field.transform, "Text", "", 40, new Color(0.15f, 0.17f, 0.25f));
            UiBuilder.Stretch((RectTransform)_pinDisplay.transform);

            // 숫자 키패드 3열 × 4행 (1~9 / ← 0 확인)
            var keypad = new GameObject("Keypad", typeof(RectTransform));
            keypad.transform.SetParent(panel.transform, false);
            UiBuilder.Place((RectTransform)keypad.transform, new Vector2(0, -60), new Vector2(480, 560));

            for (int i = 1; i <= 9; i++)
            {
                int digit = i;
                AddKey(keypad.transform, digit.ToString(), KeyPos(i - 1), new Color(0.25f, 0.28f, 0.36f),
                    () => AppendDigit(digit.ToString()));
            }
            AddKey(keypad.transform, TextLibrary.Get("admin.backspace"), KeyPos(9), new Color(0.55f, 0.45f, 0.30f), Backspace);
            AddKey(keypad.transform, "0", KeyPos(10), new Color(0.25f, 0.28f, 0.36f), () => AppendDigit("0"));
            AddKey(keypad.transform, TextLibrary.Get("admin.unlock"), KeyPos(11), new Color(0.35f, 0.75f, 0.45f), TryUnlock);

            Button cancel = UiBuilder.CreateButton(panel.transform,
                TextLibrary.Get("admin.close"), new Color(0.55f, 0.57f, 0.62f), 28);
            UiBuilder.Place((RectTransform)cancel.transform, new Vector2(0, -370), new Vector2(300, 66));
            cancel.onClick.AddListener(() => CloseRequested?.Invoke());

            _lockMessage = UiBuilder.CreateText(panel.transform, "LockMessage", "", 24, new Color(1f, 0.5f, 0.45f));
            UiBuilder.Place((RectTransform)_lockMessage.transform, new Vector2(0, 200), new Vector2(560, 36));
        }

        // 키패드 슬롯(0~11) → 좌표. 3열 × 4행, 버튼 140×110 + 간격 20
        private static Vector2 KeyPos(int slot)
        {
            int col = slot % 3;
            int row = slot / 3;
            return new Vector2(-160f + col * 160f, 195f - row * 130f);
        }

        private void AddKey(Transform parent, string label, Vector2 pos, Color color, UnityEngine.Events.UnityAction action)
        {
            Button key = UiBuilder.CreateButton(parent, label, color, 32);
            UiBuilder.Place((RectTransform)key.transform, pos, new Vector2(140, 110));
            key.onClick.AddListener(action);
        }

        private void AppendDigit(string digit)
        {
            if (_pin.Length >= 12) return; // 무한 입력 방지
            _pin += digit;
            ApplyPinDisplay();
        }

        private void Backspace()
        {
            if (_pin.Length == 0) return;
            _pin = _pin.Substring(0, _pin.Length - 1);
            ApplyPinDisplay();
        }

        private void ApplyPinDisplay()
        {
            if (_pinDisplay != null) _pinDisplay.text = new string('●', _pin.Length);
            if (_lockMessage != null) _lockMessage.text = "";
        }

        private void BuildBody(Transform parent)
        {
            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(parent, false);
            UiBuilder.Stretch((RectTransform)body.transform);
            _bodyGroup = body;

            // 좌표는 KioskCanvas 기준 1920×1080 (가시 범위 x ±950, y ±530).
            // 세로 구획: 제목·탭(y 440) / 상태·목록(y 400 ~ -280) / 페이지(-330) / 명령 행(-430) / 메시지(-505)
            Text title = UiBuilder.CreateText(body.transform, "Title",
                TextLibrary.Get("admin.title"), 44, Color.white);
            UiBuilder.Place((RectTransform)title.transform, new Vector2(-620, 440), new Vector2(560, 60));

            // 좌측: 상태 블록 (왼쪽 정렬 멀티라인)
            Image statusBox = UiBuilder.CreateImage(body.transform, "StatusBox", new Color(0.13f, 0.15f, 0.20f));
            UiBuilder.Place((RectTransform)statusBox.transform, new Vector2(-620, 55), new Vector2(580, 700));
            _statusText = UiBuilder.CreateText(statusBox.transform, "StatusText", "", 24, new Color(0.85f, 0.88f, 0.95f));
            UiBuilder.Place((RectTransform)_statusText.transform, Vector2.zero, new Vector2(530, 660));
            _statusText.alignment = TextAnchor.UpperLeft;

            // 우측: 작품 목록 (갤러리/격리 전환 + 페이지)
            Button galleryTab = UiBuilder.CreateButton(body.transform,
                TextLibrary.Get("admin.tab.gallery"), new Color(0.30f, 0.55f, 0.85f), 26);
            UiBuilder.Place((RectTransform)galleryTab.transform, new Vector2(-90, 440), new Vector2(230, 60));
            galleryTab.onClick.AddListener(() => { _showQuarantine = false; _page = 0; RefreshAll(); });

            Button quarantineTab = UiBuilder.CreateButton(body.transform,
                TextLibrary.Get("admin.tab.quarantine"), new Color(0.85f, 0.55f, 0.30f), 26);
            UiBuilder.Place((RectTransform)quarantineTab.transform, new Vector2(170, 440), new Vector2(230, 60));
            quarantineTab.onClick.AddListener(() => { _showQuarantine = true; _page = 0; RefreshAll(); });

            _listTitle = UiBuilder.CreateText(body.transform, "ListTitle", "", 28, Color.white);
            UiBuilder.Place((RectTransform)_listTitle.transform, new Vector2(600, 440), new Vector2(420, 50));

            var grid = new GameObject("Grid", typeof(RectTransform));
            grid.transform.SetParent(body.transform, false);
            UiBuilder.Place((RectTransform)grid.transform, new Vector2(300, 60), new Vector2(1200, 680));
            _grid = grid.transform;

            Button prev = UiBuilder.CreateButton(body.transform, "◀", new Color(0.35f, 0.38f, 0.45f), 30);
            UiBuilder.Place((RectTransform)prev.transform, new Vector2(-100, -330), new Vector2(90, 60));
            prev.onClick.AddListener(() => { _page--; RefreshAll(); });

            _pageLabel = UiBuilder.CreateText(body.transform, "PageLabel", "", 26, new Color(0.8f, 0.83f, 0.9f));
            UiBuilder.Place((RectTransform)_pageLabel.transform, new Vector2(300, -330), new Vector2(400, 50));

            Button next = UiBuilder.CreateButton(body.transform, "▶", new Color(0.35f, 0.38f, 0.45f), 30);
            UiBuilder.Place((RectTransform)next.transform, new Vector2(700, -330), new Vector2(90, 60));
            next.onClick.AddListener(() => { _page++; RefreshAll(); });

            // 하단 명령 버튼 행 (7개, 폭 240 + 간격 260 → 좌우 ±780 안에 들어간다)
            float y = -430f;
            AddCommand(body.transform, "admin.test", new Vector2(-780, y), new Color(0.35f, 0.75f, 0.45f), OnTestGenerate);
            AddCommand(body.transform, "admin.restart", new Vector2(-520, y), new Color(0.85f, 0.55f, 0.30f), OnRestartServer);
            AddCommand(body.transform, "admin.reload", new Vector2(-260, y), new Color(0.30f, 0.55f, 0.85f), OnReloadData);
            AddCommand(body.transform, "admin.openSessions", new Vector2(0, y), new Color(0.45f, 0.48f, 0.55f),
                () => OpenFolder(SessionStore.SessionsDir));
            AddCommand(body.transform, "admin.openLogs", new Vector2(260, y), new Color(0.45f, 0.48f, 0.55f),
                () => OpenFolder(LogManager.LogsDir));
            Button wipe = AddCommand(body.transform, "admin.wipe", new Vector2(520, y), new Color(0.80f, 0.30f, 0.30f), OnWipeGallery);
            _wipeLabel = wipe.GetComponentInChildren<Text>();
            AddCommand(body.transform, "admin.close", new Vector2(780, y), new Color(0.55f, 0.57f, 0.62f),
                () => CloseRequested?.Invoke());

            _messageText = UiBuilder.CreateText(body.transform, "Message", "", 26, new Color(1f, 0.85f, 0.30f));
            UiBuilder.Place((RectTransform)_messageText.transform, new Vector2(0, -505), new Vector2(1800, 44));
        }

        private Button AddCommand(Transform parent, string textKey, Vector2 pos, Color color, UnityEngine.Events.UnityAction action)
        {
            Button button = UiBuilder.CreateButton(parent, TextLibrary.Get(textKey), color, 22);
            UiBuilder.Place((RectTransform)button.transform, pos, new Vector2(240, 70));
            button.onClick.AddListener(action);
            return button;
        }

        // ── 진입(PIN) ────────────────────────────────────────

        private void TryUnlock()
        {
            if (_pin == ConfigManager.Config.admin.password)
            {
                _pin = "";
                ApplyPinDisplay();
                _lockGroup.SetActive(false);
                _bodyGroup.SetActive(true);
                RefreshAll();
                LogManager.Info("[Admin] 관리자 모드 진입");
                return;
            }
            _pin = "";
            ApplyPinDisplay();
            _lockMessage.text = TextLibrary.Get("admin.wrongPassword");
            LogManager.Warn("[Admin] 관리자 PIN 불일치");
        }

        // ── 상태·목록 갱신 ───────────────────────────────────

        private void RefreshAll()
        {
            RefreshStatus();
            RefreshList();
        }

        private void RefreshStatus()
        {
            AppConfig cfg = ConfigManager.Config;
            // StatusLine이 이미 "정상 (시각)" / "실패 3회 — 이유" 형태라 상태어를 앞에 또 붙이지 않는다
            string comfy = _watchdog != null
                ? (_watchdog.IsHealthy ? _watchdog.StatusLine : $"무응답 — {_watchdog.StatusLine}")
                : "워치독 없음";
            string uploader = _b2 != null && _b2.IsConfigured ? "Backblaze B2 (연결됨)"
                : _gcs != null && _gcs.IsConfigured ? "GCS (연결됨)"
                : "미설정 — QR 자동 숨김";

            _statusText.text =
                $"버전: {Application.version} (Unity {Application.unityVersion})\n\n" +
                $"ComfyUI: {comfy}\n" +
                $"  주소: {cfg.comfyUi.baseUrl}\n\n" +
                $"업로더(QR): {uploader}\n" +
                $"필터(VLM): {(cfg.filter.enabled ? $"켜짐 — {cfg.filter.model}" : "꺼짐 (opt-in 즉시 전시)")}\n" +
                $"영상 생성: {(cfg.video.enabled ? "켜짐" : "꺼짐")}\n" +
                $"워치독: {(cfg.watchdog.enabled ? $"켜짐 ({cfg.watchdog.checkIntervalSeconds:F0}초 주기)" : "꺼짐")}\n\n" +
                $"스타일: {StyleLibrary.Styles.Count}종\n" +
                $"갤러리: {CountPngs(SessionStore.GalleryDir)}점\n" +
                $"격리: {CountPngs(SessionStore.QuarantineDir)}점\n" +
                $"세션 기록: {CountPngs(SessionStore.SessionsDir, "*_result.png")}건";
        }

        private void RefreshList()
        {
            ClearThumbnails();

            string dir = _showQuarantine ? SessionStore.QuarantineDir : SessionStore.GalleryDir;
            string[] files = ListPngs(dir);
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(files.Length / (float)PageSize));
            _page = Mathf.Clamp(_page, 0, pageCount - 1);

            _listTitle.text = _showQuarantine
                ? $"{TextLibrary.Get("admin.tab.quarantine")} ({files.Length})"
                : $"{TextLibrary.Get("admin.tab.gallery")} ({files.Length})";
            _pageLabel.text = files.Length == 0
                ? TextLibrary.Get("admin.listEmpty")
                : $"{_page + 1} / {pageCount}";

            for (int i = 0; i < PageSize; i++)
            {
                int index = _page * PageSize + i;
                if (index >= files.Length) break;
                CreateCell(files[index], i);
            }
        }

        // 썸네일 한 칸 = 이미지 + (갤러리: 삭제 / 격리: 복원·삭제) 버튼
        private void CreateCell(string path, int slot)
        {
            int col = slot % Columns;
            int row = slot / Columns;
            // 그리드(중심 300,60 / 1200×680) 안의 배치: 칸 폭 270 + 열 간격 290, 행 간격 320
            var pos = new Vector2(-435f + col * 290f, 160f - row * 320f);

            Image frame = UiBuilder.CreateImage(_grid, "Cell", new Color(0.16f, 0.18f, 0.24f));
            UiBuilder.Place((RectTransform)frame.transform, pos, new Vector2(270, 300));
            _cells.Add(frame.gameObject);

            RawImage thumb = UiBuilder.CreateRawImage(frame.transform, "Thumb");
            UiBuilder.Place((RectTransform)thumb.transform, new Vector2(0, 60), new Vector2(250, 167)); // 3:2 유지
            Texture2D tex = LoadThumbnail(path);
            if (tex != null) { thumb.texture = tex; _thumbnails.Add(tex); }

            Text name = UiBuilder.CreateText(frame.transform, "Name",
                Path.GetFileNameWithoutExtension(path).Replace("_result", ""), 20, new Color(0.75f, 0.78f, 0.85f));
            UiBuilder.Place((RectTransform)name.transform, new Vector2(0, -55), new Vector2(250, 30));

            if (_showQuarantine)
            {
                Button restore = UiBuilder.CreateButton(frame.transform,
                    TextLibrary.Get("admin.restore"), new Color(0.35f, 0.75f, 0.45f), 20);
                UiBuilder.Place((RectTransform)restore.transform, new Vector2(-62, -105), new Vector2(120, 48));
                restore.onClick.AddListener(() => OnRestore(path));

                Button delete = UiBuilder.CreateButton(frame.transform,
                    TextLibrary.Get("admin.delete"), new Color(0.80f, 0.30f, 0.30f), 20);
                UiBuilder.Place((RectTransform)delete.transform, new Vector2(62, -105), new Vector2(120, 48));
                delete.onClick.AddListener(() => OnDelete(path));
            }
            else
            {
                Button delete = UiBuilder.CreateButton(frame.transform,
                    TextLibrary.Get("admin.delete"), new Color(0.80f, 0.30f, 0.30f), 20);
                UiBuilder.Place((RectTransform)delete.transform, new Vector2(0, -105), new Vector2(250, 48));
                delete.onClick.AddListener(() => OnDelete(path));
            }
        }

        private Texture2D LoadThumbnail(string path)
        {
            try
            {
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(File.ReadAllBytes(path))) return tex;
                Destroy(tex);
            }
            catch (Exception e)
            {
                LogManager.Warn($"[Admin] 썸네일 로드 실패: {Path.GetFileName(path)} — {e.Message}");
            }
            return null;
        }

        private void ClearThumbnails()
        {
            foreach (GameObject cell in _cells) if (cell != null) Destroy(cell);
            _cells.Clear();
            foreach (Texture2D tex in _thumbnails) if (tex != null) Destroy(tex);
            _thumbnails.Clear();
        }

        // ── 동작 ─────────────────────────────────────────────

        private void OnDelete(string path)
        {
            try
            {
                File.Delete(path);
                SetMessage($"{TextLibrary.Get("admin.deleted")} {Path.GetFileName(path)}");
                LogManager.Info($"[Admin] 작품 삭제: {path}");
            }
            catch (Exception e)
            {
                SetMessage($"삭제 실패: {e.Message}");
                LogManager.Error($"[Admin] 삭제 실패: {e.Message}");
            }
            RefreshAll();
        }

        // 격리 → 갤러리 복원 (계획서 11장). 원본은 Sessions에 남아 있으므로 이동으로 처리한다
        private void OnRestore(string path)
        {
            try
            {
                Directory.CreateDirectory(SessionStore.GalleryDir);
                string dest = Path.Combine(SessionStore.GalleryDir, Path.GetFileName(path));
                File.Copy(path, dest, true);
                File.Delete(path);
                SetMessage($"{TextLibrary.Get("admin.restored")} {Path.GetFileName(path)}");
                LogManager.Info($"[Admin] 격리 복원 → 갤러리: {dest}");
            }
            catch (Exception e)
            {
                SetMessage($"복원 실패: {e.Message}");
                LogManager.Error($"[Admin] 복원 실패: {e.Message}");
            }
            RefreshAll();
        }

        private void OnTestGenerate()
        {
            if (_comfyClient == null) { SetMessage("ComfyUI 클라이언트를 찾지 못했습니다"); return; }
            SetMessage(TextLibrary.Get("admin.testRunning"));
            _comfyClient.TestGenerate((ok, message) =>
            {
                SetMessage(message);
                LogManager.Info($"[Admin] {message}");
                RefreshStatus();
            });
        }

        private void OnRestartServer()
        {
            if (_watchdog == null) { SetMessage("워치독을 찾지 못했습니다"); return; }
            bool started = _watchdog.RestartNow();
            SetMessage(started ? TextLibrary.Get("admin.restarting") : "재시작을 시작하지 못했습니다 (로그 확인)");
            RefreshStatus();
        }

        // 설정·스타일·문구 JSON을 다시 읽는다 (계획서 11장). 운영 중 프롬프트만 고쳐 다음 생성부터 반영할 때 쓴다
        private void OnReloadData()
        {
            ConfigManager.Reload();
            StyleLibrary.Reload();
            TextLibrary.Reload();
            SetMessage(TextLibrary.Get("admin.reloaded"));
            LogManager.Info("[Admin] Config/Styles/Texts 다시 읽음");
            RefreshAll();
        }

        // 갤러리·격리 비우기. 두 번 눌러야 실행된다 (Sessions 기록은 건드리지 않는다)
        private void OnWipeGallery()
        {
            if (!_wipeArmed)
            {
                _wipeArmed = true;
                if (_wipeLabel != null) _wipeLabel.text = TextLibrary.Get("admin.wipeConfirm");
                SetMessage(TextLibrary.Get("admin.wipeWarn"));
                return;
            }

            _wipeArmed = false;
            if (_wipeLabel != null) _wipeLabel.text = TextLibrary.Get("admin.wipe");
            int removed = DeleteAllPngs(SessionStore.GalleryDir) + DeleteAllPngs(SessionStore.QuarantineDir);
            SetMessage($"{TextLibrary.Get("admin.wiped")} ({removed}점)");
            LogManager.Info($"[Admin] 갤러리·격리 초기화: {removed}점 삭제");
            RefreshAll();
        }

        private void OpenFolder(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                Application.OpenURL("file:///" + dir.Replace('\\', '/'));
                SetMessage(dir);
            }
            catch (Exception e)
            {
                SetMessage($"폴더 열기 실패: {e.Message}");
            }
        }

        private void SetMessage(string message)
        {
            if (_messageText != null) _messageText.text = message;
        }

        // ── 파일 헬퍼 ────────────────────────────────────────

        private static string[] ListPngs(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return Array.Empty<string>();
                string[] files = Directory.GetFiles(dir, "*.png");
                Array.Sort(files, StringComparer.Ordinal);
                Array.Reverse(files); // 최신 세션 ID가 앞으로 (파일명이 yyyyMMdd_HHmmss)
                return files;
            }
            catch (Exception e)
            {
                LogManager.Error($"[Admin] 폴더 조회 실패({dir}): {e.Message}");
                return Array.Empty<string>();
            }
        }

        private static int CountPngs(string dir, string pattern = "*.png")
        {
            try { return Directory.Exists(dir) ? Directory.GetFiles(dir, pattern).Length : 0; }
            catch { return 0; }
        }

        private static int DeleteAllPngs(string dir)
        {
            int count = 0;
            foreach (string path in ListPngs(dir))
            {
                try { File.Delete(path); count++; }
                catch (Exception e) { LogManager.Error($"[Admin] 삭제 실패({path}): {e.Message}"); }
            }
            return count;
        }
    }
}
