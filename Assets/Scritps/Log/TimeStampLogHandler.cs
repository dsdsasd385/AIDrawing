using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public class TimeStampLogHandler : ILogHandler
{
    #region private 변수

    private readonly ILogHandler _defaultHandler;
    private readonly Func<DateTime> _nowProvider;
    private readonly string _dateFormat;
    private readonly string _timeFormat;

    #endregion

    /// <param name="defaultHandler">원래의 Unity 로그 핸들러 (필수)</param>
    /// <param name="nowProvider">시간 제공자 (테스트/UTC 전환용, 기본: DateTime.Now)</param>
    /// <param name="dateFormat">날짜 포맷 (기본: "yyyy-MM-dd")</param>
    /// <param name="timeFormat">시간 포맷 (기본: "HH:mm:ss")</param>
    public TimeStampLogHandler(ILogHandler defaultHandler,
        Func<DateTime> nowProvider = null,
        string dateFormat = "yyyy-MM-dd",
        string timeFormat = "HH:mm:ss")
    {
        _defaultHandler = defaultHandler ?? Debug.unityLogger.logHandler;
        _nowProvider = nowProvider ?? (() => DateTime.Now);
        _dateFormat = string.IsNullOrEmpty(dateFormat) ? "yyyy-MM-dd" : dateFormat;
        _timeFormat = string.IsNullOrEmpty(timeFormat) ? "HH:mm:ss" : timeFormat;
    }

    public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
    {
        var now = _nowProvider();
        // 원본 포맷 문자열 앞에 [날짜] [시간] 을 붙여서 넘깁니다.
        _defaultHandler.LogFormat(logType, context,
            $"[{now.ToString(_dateFormat)}] [{now.ToString(_timeFormat)}] {format}", args);
    }

    public void LogException(Exception exception, UnityEngine.Object context)
    {
        // 예외 메시지에도 타임스탬프를 붙이고 싶다면 여기서 가공 가능
        // 기본 핸들러에 그대로 위임
        _defaultHandler.LogException(exception, context);
    }
}