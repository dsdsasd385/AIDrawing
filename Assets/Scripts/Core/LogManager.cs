using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EarthCoding.Core
{
    /// <summary>
    /// 로그 기록 매니저. (작업계획서 20장 '오류 대응' 대응)
    /// Unity 의 모든 로그(Debug.Log / Warning / Error / Exception)를 받아
    /// 실행 파일 옆 Log 폴더에 날짜별 텍스트 파일로 기록한다.
    /// 전시장 운영 중 발생한 오류를 유지보수에 활용하기 위한 시스템이며,
    /// GameManager 가 가장 먼저 초기화한다.
    /// </summary>
    public static class LogManager
    {
        /// <summary>로그 파일이 저장되는 폴더 절대 경로</summary>
        public static string LogDirectory { get; private set; }

        /// <summary>관리자 모드 '로그 확인' 화면에 보여줄 최근 로그 (메모리 보관)</summary>
        private static readonly List<string> _recentLogs = new List<string>();

        /// <summary>메모리에 보관할 최근 로그 최대 개수 (관리자 화면 표시용)</summary>
        private const int MaxRecentLogs = 200;

        /// <summary>중복 초기화 방지 플래그</summary>
        private static bool _initialized;

        /// <summary>
        /// 로그 시스템을 초기화한다. Log 폴더가 없으면 자동 생성하고
        /// Unity 로그 콜백을 등록한다. 여러 번 호출해도 안전하다.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // 에디터에서는 프로젝트 루트, 빌드에서는 exe 옆에 Log 폴더를 만든다
            var root = Application.isEditor
                ? Directory.GetParent(Application.dataPath).FullName
                : Path.GetDirectoryName(Application.dataPath);
            LogDirectory = Path.Combine(root, "Log");

            try
            {
                // 설정 폴더가 없을 경우 자동 생성 (작업계획서 23장 자동 복구)
                if (!Directory.Exists(LogDirectory))
                    Directory.CreateDirectory(LogDirectory);
            }
            catch (Exception e)
            {
                // 로그 폴더 생성 실패 시에도 프로그램은 계속 동작해야 한다
                Debug.LogWarning($"[LogManager] Log 폴더 생성 실패: {e.Message}");
                LogDirectory = null;
            }

            // Unity 의 모든 로그를 파일로도 남기기 위한 콜백 등록
            Application.logMessageReceived += OnLogMessage;
            Write("Info", "===== 프로그램 시작 =====");
        }

        /// <summary>
        /// 최근 로그 목록을 반환한다. 관리자 모드의 '로그 확인' 기능에서 사용한다.
        /// </summary>
        public static IReadOnlyList<string> RecentLogs => _recentLogs;

        /// <summary>
        /// 마지막 실행에서 기록된 오늘자 로그 파일 경로를 반환한다.
        /// 실행 시 마지막 오류 확인(작업계획서 23장)에 사용한다.
        /// </summary>
        public static string TodayLogFilePath =>
            LogDirectory == null ? null : Path.Combine(LogDirectory, $"log_{DateTime.Now:yyyyMMdd}.txt");

        /// <summary>Unity 로그 콜백. 로그를 파일과 메모리 목록에 기록한다.</summary>
        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            // 오류/예외는 스택트레이스까지 남겨 유지보수에 활용한다
            var message = (type == LogType.Error || type == LogType.Exception)
                ? $"{condition}\n{stackTrace}"
                : condition;
            Write(type.ToString(), message);
        }

        /// <summary>
        /// 로그 한 줄을 파일과 메모리에 기록한다.
        /// </summary>
        /// <param name="level">로그 레벨 문자열 (Info/Warning/Error 등)</param>
        /// <param name="message">로그 내용</param>
        public static void Write(string level, string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";

            // 관리자 화면 표시용 메모리 보관 (최대 개수 초과 시 오래된 것부터 제거)
            _recentLogs.Add(line);
            if (_recentLogs.Count > MaxRecentLogs)
                _recentLogs.RemoveAt(0);

            if (LogDirectory == null) return;

            try
            {
                File.AppendAllText(TodayLogFilePath, line + Environment.NewLine);
            }
            catch
            {
                // 파일 쓰기 실패(디스크 오류 등)가 프로그램을 멈추면 안 되므로 무시한다
            }
        }
    }
}
