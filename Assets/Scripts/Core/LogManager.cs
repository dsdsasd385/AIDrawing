using System;
using System.IO;
using UnityEngine;

namespace CarDrawing.Core
{
    /// <summary>
    /// 파일 로그 관리자. 계획서 12장: 모든 오류는 Logs/ 폴더에 파일로 기록한다.
    /// Unity 콘솔의 Error/Exception은 훅으로 자동 수집한다 (전시장 무인 운영 중 사후 진단용).
    /// 코어 시스템에 속하며 모든 시스템이 공용으로 사용한다.
    /// </summary>
    public static class LogManager
    {
        private static readonly object WriteLock = new object();

        /// <summary>로그 폴더 (persistentDataPath/Logs, 계획서 9장 폴더 구조)</summary>
        public static string LogsDir => Path.Combine(Application.persistentDataPath, "Logs");

        // 플레이 시작 시 Unity 로그 훅을 1회 연결한다 (도메인 리로드 대비 중복 해제 후 등록)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookUnityLog()
        {
            Application.logMessageReceived -= OnUnityLog;
            Application.logMessageReceived += OnUnityLog;
        }

        private static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            // Error/Exception만 훅이 파일에 남긴다. Info/Warning은 각 메서드가 직접 기록 (중복 방지)
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                WriteLine("ERROR", string.IsNullOrEmpty(stackTrace) ? condition : condition + "\n" + stackTrace);
        }

        /// <summary>일반 정보를 콘솔과 파일에 기록한다.</summary>
        public static void Info(string message)
        {
            Debug.Log(message);
            WriteLine("INFO", message);
        }

        /// <summary>경고를 콘솔과 파일에 기록한다.</summary>
        public static void Warn(string message)
        {
            Debug.LogWarning(message);
            WriteLine("WARN", message);
        }

        /// <summary>오류를 기록한다. 파일 기록은 Unity 로그 훅이 담당하므로 콘솔 출력만 한다.</summary>
        public static void Error(string message)
        {
            Debug.LogError(message);
        }

        private static void WriteLine(string level, string message)
        {
            try
            {
                Directory.CreateDirectory(LogsDir);
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}\n";
                lock (WriteLock)
                    File.AppendAllText(Path.Combine(LogsDir, DateTime.Now.ToString("yyyyMMdd") + ".log"), line);
            }
            catch
            {
                // 로그 기록 실패(디스크 문제 등)가 앱을 죽여서는 안 된다 — 조용히 무시
            }
        }
    }
}
