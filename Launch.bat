@echo off
setlocal enabledelayedexpansion
title OmniClean - Storage Optimizer
cd /d "%~dp0"

:: ---------------------------------------------------------------
:: Usage:  Launch.bat           start engine (if needed) + open UI
::         Launch.bat stop      stop the background engine
::         Launch.bat restart   stop, then start fresh
:: ---------------------------------------------------------------

if /i "%~1"=="stop" goto :stop
if /i "%~1"=="restart" call "%~f0" stop

:: --- Preflight checks ---
where node >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Node.js was not found on this system.
    echo         Install it from https://nodejs.org/ and try again.
    pause
    exit /b 1
)
if not exist "server.js" (
    echo [ERROR] server.js was not found next to Launch.bat.
    pause
    exit /b 1
)

:: --- Start the local engine if it is not already listening ---
:: Note: the trailing space in the findstr pattern prevents matching
:: ports like 49820 when looking for 4982.
netstat -ano | findstr /C:"127.0.0.1:4982 " | findstr LISTENING >nul
if errorlevel 1 (
    echo Starting OmniClean background engine...
    del /q "server.log" >nul 2>&1
    start "OmniClean Engine" /min cmd /c "node server.js >> server.log 2>&1"
    timeout /t 2 /nobreak >nul
    netstat -ano | findstr /C:"127.0.0.1:4982 " | findstr LISTENING >nul
    if errorlevel 1 (
        echo [ERROR] The engine failed to start. Last log lines:
        if exist "server.log" powershell -NoProfile -Command "Get-Content -Path 'server.log' -Tail 10"
        pause
        exit /b 1
    )
) else (
    echo OmniClean engine already running.
)

:: --- Open in native app window mode ---
if exist "C:\Program Files\Google\Chrome\Application\chrome.exe" (
    start "" "C:\Program Files\Google\Chrome\Application\chrome.exe" --app=http://127.0.0.1:4982 --window-size=1200,820
) else if exist "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" (
    start "" "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --app=http://127.0.0.1:4982 --window-size=1200,820
) else (
    start "" http://127.0.0.1:4982
)
exit /b 0

:stop
set "ENGINE_PID="
for /f "tokens=5" %%P in ('netstat -ano ^| findstr /C:"127.0.0.1:4982 " ^| findstr LISTENING') do (
    set "ENGINE_PID=%%P"
)
if defined ENGINE_PID (
    taskkill /PID !ENGINE_PID! /F >nul 2>&1
    echo OmniClean engine stopped.
) else (
    echo OmniClean engine is not running.
)
exit /b 0
