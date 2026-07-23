using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using CarDrawing.Core;
using UnityEditor;
using UnityEngine;

namespace CarDrawing.Editor
{
    /// <summary>
    /// Unity 프로젝트를 열었을 때 로컬 ComfyUI가 꺼져 있으면 기존 운영 스크립트로 한 번만 기동한다.
    /// 플레이 모드 워치독보다 앞서 서버를 준비해 개발자가 매번 배치 파일을 직접 켜지 않게 한다.
    /// </summary>
    [InitializeOnLoad]
    internal static class ComfyUIEditorAutoStart
    {
        private const string LastRequestKey = "CarDrawing.ComfyUIEditorAutoStart.LastRequest";
        private const int RetrySeconds = 180;

        static ComfyUIEditorAutoStart()
        {
            EditorApplication.delayCall += () => TryStart(false);
        }

        [MenuItem("Tools/AI Car Drawing/Start ComfyUI Server")]
        private static void StartFromMenu()
        {
            if (!TryStart(true))
                UnityEngine.Debug.Log("[ComfyUI Editor] 서버가 이미 실행 중이거나 시작 조건을 충족하지 않음");
        }

        /// <summary>자동 시작과 동일한 경로를 즉시 실행한다. 검증·관리 도구에서 사용할 수 있다.</summary>
        public static bool StartNow() => TryStart(true);

        private static bool TryStart(bool force)
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return false;

            WatchdogConfig cfg = ConfigManager.Config.watchdog;
            if (!force && (!cfg.enabled || !cfg.editorAutoStart)) return false;
            if (IsServerListening(ConfigManager.Config.comfyUi.baseUrl)) return false;

            int now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int lastRequest = SessionState.GetInt(LastRequestKey, 0);
            if (!force && now - lastRequest < RetrySeconds) return false;

            string script = ResolveProjectPath(cfg.restartCommand);
            if (string.IsNullOrEmpty(script) || !File.Exists(script))
            {
                UnityEngine.Debug.LogError($"[ComfyUI Editor] 자동 시작 스크립트 없음: {script}");
                return false;
            }

            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = script,
                    WorkingDirectory = Path.GetDirectoryName(script) ?? string.Empty,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(info);
                SessionState.SetInt(LastRequestKey, now);
                UnityEngine.Debug.Log($"[ComfyUI Editor] 서버 자동 시작 요청: {script}");
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[ComfyUI Editor] 서버 자동 시작 실패: {e.Message}");
                return false;
            }
        }

        private static bool IsServerListening(string baseUrl)
        {
            try
            {
                var uri = new Uri(baseUrl);
                int port = uri.IsDefaultPort ? 8188 : uri.Port;
                using (var client = new TcpClient())
                {
                    IAsyncResult connect = client.BeginConnect(uri.Host, port, null, null);
                    bool connected;
                    using (var waitHandle = connect.AsyncWaitHandle)
                        connected = waitHandle.WaitOne(400);
                    if (connected) client.EndConnect(connect);
                    return connected && client.Connected;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveProjectPath(string configured)
        {
            if (string.IsNullOrEmpty(configured)) return null;
            if (Path.IsPathRooted(configured)) return configured;
            return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, configured));
        }
    }
}
