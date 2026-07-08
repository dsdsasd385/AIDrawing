using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class InstallTimeStampLogger
{
    #region private 변수

    private static bool _installed;

    #endregion

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // 이미 할당됐다면 실행X
        if (_installed)
        {
            return;
        }

        var original = Debug.unityLogger.logHandler;

        // 필요에 따라 UTC/밀리초 등 포맷 변경 가능
        // 예) timeFormat: "HH:mm:ss.fff" (밀리초 포함), UTC 사용: () => DateTime.UtcNow
        var handler = new TimeStampLogHandler(original,
            nowProvider: () => DateTime.Now,
            dateFormat: "yyyy-MM-dd",
            timeFormat: "HH:mm:ss");

        Debug.unityLogger.logHandler = handler;
        _installed = true;
    }
}