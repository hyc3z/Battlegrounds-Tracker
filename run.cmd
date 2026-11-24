@echo off
rem One-click build & run for the Battlegrounds Tracker.
rem Usage: run.cmd [Debug^|Release] [-NoBuild] [-Wait]
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1" %*
if errorlevel 1 (
    echo.
    echo Build or launch failed.
    pause
)
