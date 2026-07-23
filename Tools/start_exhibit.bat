@echo off
rem Exhibition auto-start: ComfyUI first, then the Unity app once the server answers.
rem NOTE: keep this file ASCII-only. Korean/UTF-8 comments break cmd.exe line
rem       parsing under cp949 and the script silently does nothing (see handover doc sec.6).
rem
rem Register with Task Scheduler (run at logon, highest privileges) or drop a
rem shortcut in shell:startup. Details: handover doc sec.7.
rem
rem APP_EXE is auto-detected. The exe name follows Unity productName (currently
rem AICarDrawing.exe); older builds were CarDrawing.exe, so both names are tried,
rem in both layouts:
rem   deployed <exedir>\Tools\start_exhibit.bat   -> <exedir>\<name>.exe
rem   dev      <project>\Tools\start_exhibit.bat  -> <project>\Build\<name>.exe
rem If the exe is missing this script still starts ComfyUI and exits, so an
rem operator can launch the app from the Editor.

setlocal
set SCRIPT_DIR=%~dp0
set APP_EXE=%SCRIPT_DIR%..\AICarDrawing.exe
if not exist "%APP_EXE%" set APP_EXE=%SCRIPT_DIR%..\Build\AICarDrawing.exe
if not exist "%APP_EXE%" set APP_EXE=%SCRIPT_DIR%..\CarDrawing.exe
if not exist "%APP_EXE%" set APP_EXE=%SCRIPT_DIR%..\Build\CarDrawing.exe
set HEALTH_URL=http://127.0.0.1:8188/system_stats
set MAX_WAIT_SECONDS=180
set LOGFILE=%SCRIPT_DIR%start_exhibit.log

echo [%date% %time%] start_exhibit begin >> "%LOGFILE%"

rem 1) ComfyUI (run_comfyui.bat blocks, so start it in its own window)
start "ComfyUI" /min cmd /c "%SCRIPT_DIR%run_comfyui.bat"
echo [%date% %time%] ComfyUI launch requested >> "%LOGFILE%"

rem 2) Wait until the server answers /system_stats (cold boot takes ~90s)
set /a WAITED=0
:WAIT_LOOP
powershell -NoProfile -Command "try { $r = Invoke-WebRequest -Uri '%HEALTH_URL%' -TimeoutSec 3 -UseBasicParsing; if ($r.StatusCode -eq 200) { exit 0 } else { exit 1 } } catch { exit 1 }"
if %ERRORLEVEL%==0 goto SERVER_READY
set /a WAITED+=5
if %WAITED% GEQ %MAX_WAIT_SECONDS% goto SERVER_TIMEOUT
timeout /t 5 /nobreak > nul
goto WAIT_LOOP

:SERVER_READY
echo [%date% %time%] ComfyUI ready after %WAITED%s >> "%LOGFILE%"
goto LAUNCH_APP

:SERVER_TIMEOUT
rem The app must start anyway: it shows the "preparing" notice and the watchdog
rem keeps retrying the restart. An exhibit with a black screen is worse.
echo [%date% %time%] WARNING ComfyUI not ready after %MAX_WAIT_SECONDS%s - starting app anyway >> "%LOGFILE%"

:LAUNCH_APP
if not exist "%APP_EXE%" (
  echo [%date% %time%] ERROR app exe not found: %APP_EXE% >> "%LOGFILE%"
  goto END
)
echo [%date% %time%] launching app: %APP_EXE% >> "%LOGFILE%"
start "AICarDrawing" "%APP_EXE%"

:END
echo [%date% %time%] start_exhibit end >> "%LOGFILE%"
endlocal
