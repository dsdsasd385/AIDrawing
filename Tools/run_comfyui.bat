@echo off
rem ComfyUI 서버 실행 스크립트
rem - 전시장 부팅 자동 시작 시 이 파일을 작업 스케줄러/시작 프로그램에 등록한다
rem - Unity 앱(ComfyUIWatchdog)도 서버 무응답 시 이 스크립트로 재시작한다
cd /d C:\Users\HULIAC\ComfyUI
venv\Scripts\python.exe main.py --listen 127.0.0.1 --port 8188
