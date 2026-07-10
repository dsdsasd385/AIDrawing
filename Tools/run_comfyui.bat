@echo off
rem ComfyUI server launcher (exhibition auto-start / watchdog restart)
rem NOTE: keep this file ASCII-only. Korean/UTF-8 comments break cmd.exe
rem       line parsing under cp949 and the script silently does nothing
rem       (verified 2026-07-10). Details: Assets/Docs .. handover doc sec.6
rem Output goes to ComfyUI\comfyui_run.log so crashes leave evidence.
rem PYTHONIOENCODING=utf-8 prevents UnicodeEncodeError on cp949 consoles.
set PYTHONIOENCODING=utf-8
set PYTHONUTF8=1
cd /d C:\Users\HULIAC\ComfyUI
echo [%date% %time%] ComfyUI starting >> comfyui_run.log
venv\Scripts\python.exe main.py --listen 127.0.0.1 --port 8188 >> comfyui_run.log 2>&1
echo [%date% %time%] ComfyUI exited (see log above for errors) >> comfyui_run.log
