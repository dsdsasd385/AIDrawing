using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using CarDrawing.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace CarDrawing.Generation
{
    /// <summary>
    /// ComfyUI 서버 헬스체크 + 재시작 (계획서 12장 워치독).
    /// 생성 시스템에 속하며 AppFlowManager가 상태를 구독해 서버가 죽은 동안 새 체험 시작을 막는다.
    /// 앱은 서버가 죽어도 계속 살아 있어야 하므로, 여기서 하는 일은
    /// ①주기적 /system_stats 확인 ②연속 실패 시 재시작 스크립트 실행 ③복구되면 모델 재예열 뿐이다.
    /// </summary>
    public class ComfyUIWatchdog : MonoBehaviour
    {
        /// <summary>서버 상태가 바뀔 때 (true=응답함). 대기 화면 안내·생성 잠금이 이 이벤트를 따른다</summary>
        public event Action<bool> HealthChanged;

        /// <summary>현재 서버가 응답하는지. 첫 체크 전에는 낙관적으로 true — 기동 직후 잠깐 안내가 뜨는 것을 피한다</summary>
        public bool IsHealthy { get; private set; } = true;

        /// <summary>관리자 화면에 보여줄 마지막 점검 결과 한 줄</summary>
        public string StatusLine { get; private set; } = "점검 전";

        [SerializeField] private ComfyUIClient comfyClient;

        private int _consecutiveFails;
        private float _lastRestartAt = float.NegativeInfinity;
        private bool _restarting;

        private void Start()
        {
            if (comfyClient == null) comfyClient = FindObjectOfType<ComfyUIClient>(true);
            StartCoroutine(CheckRoutine());
        }

        private IEnumerator CheckRoutine()
        {
            while (true)
            {
                WatchdogConfig cfg = ConfigManager.Config.watchdog;
                if (!cfg.enabled)
                {
                    // 꺼져 있으면 아무것도 안 한다. 잠겨 있던 상태는 풀어 준다 (설정을 껐는데 계속 막히면 안 됨)
                    SetHealthy(true, "워치독 꺼짐");
                    yield return new WaitForSecondsRealtime(Mathf.Max(1f, cfg.checkIntervalSeconds));
                    continue;
                }

                yield return Ping(cfg);
                yield return new WaitForSecondsRealtime(Mathf.Max(1f, cfg.checkIntervalSeconds));
            }
        }

        private IEnumerator Ping(WatchdogConfig cfg)
        {
            string url = ConfigManager.Config.comfyUi.baseUrl.TrimEnd('/') + "/system_stats";
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = Mathf.Max(1, Mathf.RoundToInt(cfg.requestTimeoutSeconds));
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    _consecutiveFails = 0;
                    SetHealthy(true, $"정상 ({DateTime.Now:HH:mm:ss})");
                    yield break;
                }

                _consecutiveFails++;
                string reason = request.error ?? "무응답";
                StatusLine = $"실패 {_consecutiveFails}회 — {reason} ({DateTime.Now:HH:mm:ss})";

                if (_consecutiveFails < Mathf.Max(1, cfg.failThreshold)) yield break;

                // 임계치 도달 — 생성 잠금 + 재시작 시도
                SetHealthy(false, StatusLine);
                TryRestart(cfg);
            }
        }

        private void SetHealthy(bool healthy, string status)
        {
            StatusLine = status;
            if (IsHealthy == healthy) return;

            IsHealthy = healthy;
            LogManager.Info($"[Watchdog] ComfyUI 상태 변경: {(healthy ? "정상" : "무응답")} — {status}");
            HealthChanged?.Invoke(healthy);

            // 재시작된 서버는 모델이 안 올라와 있다 — 첫 관람객이 콜드 로드를 만나지 않도록 다시 예열한다
            if (healthy && comfyClient != null) comfyClient.Rewarm();
        }

        /// <summary>재시작 스크립트를 실행한다 (쿨다운 안이면 건너뛴다). 관리자 모드에서도 수동으로 부른다</summary>
        /// <param name="force">쿨다운을 무시할지 (관리자 수동 재시작)</param>
        /// <returns>실제로 실행했으면 true</returns>
        public bool TryRestart(WatchdogConfig cfg, bool force = false)
        {
            if (_restarting) return false;
            if (string.IsNullOrEmpty(cfg.restartCommand))
            {
                LogManager.Warn("[Watchdog] 재시작 명령이 비어 있음 — 안내만 표시하고 재시작은 생략");
                return false;
            }
            if (!force && Time.unscaledTime - _lastRestartAt < cfg.restartCooldownSeconds)
                return false; // 기동 중일 수 있다 — 폭주 방지

            string script = ResolvePath(cfg.restartCommand);
            if (!File.Exists(script))
            {
                LogManager.Error($"[Watchdog] 재시작 스크립트 없음: {script}");
                return false;
            }

            _lastRestartAt = Time.unscaledTime;
            try
            {
                // 창 없이 백그라운드로 띄운다. 스크립트가 로그를 ComfyUI\comfyui_run.log에 남긴다 (인수인계 §6)
                var info = new ProcessStartInfo
                {
                    FileName = script,
                    WorkingDirectory = Path.GetDirectoryName(script) ?? string.Empty,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(info);
                _restarting = true;
                LogManager.Info($"[Watchdog] ComfyUI 재시작 시도: {script}");
                StartCoroutine(ClearRestartingAfter(cfg.restartCooldownSeconds));
                return true;
            }
            catch (Exception e)
            {
                // 재시작 실패해도 앱은 계속 산다 — 다음 주기에 다시 시도한다 (계획서 12장)
                LogManager.Error($"[Watchdog] 재시작 실패: {e.Message}");
                return false;
            }
        }

        /// <summary>관리자 모드용 — 현재 설정으로 강제 재시작</summary>
        public bool RestartNow() => TryRestart(ConfigManager.Config.watchdog, true);

        private IEnumerator ClearRestartingAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(10f, seconds));
            _restarting = false;
        }

        // 상대 경로는 exe 옆(에디터에서는 프로젝트 루트) 기준으로 푼다 — 업로더 키 파일과 같은 규칙
        private static string ResolvePath(string configured)
        {
            if (string.IsNullOrEmpty(configured)) return null;
            if (Path.IsPathRooted(configured)) return configured;
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, configured);
        }
    }
}
