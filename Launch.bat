@echo off
title OmniClean - Storage Optimizer
cd /d "%~dp0"

:: Check if server is already running on port 4982
netstat -ano | findstr :4982 | findstr LISTENING >nul
if %errorlevel% neq 0 (
    echo Starting OmniClean background engine...
    start "" /b node server.js
    timeout /t 1 /nobreak >nul
)

:: Launch in native app window mode
if exist "C:\Program Files\Google\Chrome\Application\chrome.exe" (
    start "" "C:\Program Files\Google\Chrome\Application\chrome.exe" --app=http://127.0.0.1:4982 --window-size=1200,820
) else if exist "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" (
    start "" "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --app=http://127.0.0.1:4982 --window-size=1200,820
) else (
    start http://127.0.0.1:4982
)

exit
