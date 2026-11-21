@echo off
rem Builds the Battlegrounds Tracker. Wraps build.ps1 so it works without changing the execution policy.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
