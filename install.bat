@echo off
setlocal
title SonarAutoSwitch - Installer
echo ========================================================
echo          SonarAutoSwitch 1-Click Installer
echo ========================================================
echo.

set "TARGET_DIR=%LOCALAPPDATA%\SonarAutoSwitch"
set "STARTUP_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"
set "SHORTCUT=%STARTUP_DIR%\SonarAutoSwitch.lnk"

echo [1/5] Closing any running instances...
taskkill /F /IM SonarAutoSwitch.exe >nul 2>&1
timeout /t 1 /nobreak >nul

echo [2/5] Compiling application...
call "%~dp0build.bat"
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Compilation failed. Aborting installation.
    pause
    exit /b 1
)

echo [3/5] Deploying files to %TARGET_DIR%...
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"
copy /Y "%~dp0bin\SonarAutoSwitch.exe" "%TARGET_DIR%\SonarAutoSwitch.exe" >nul

echo [4/5] Setting up Windows Startup shortcut...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%SHORTCUT%'); $s.TargetPath = '%TARGET_DIR%\SonarAutoSwitch.exe'; $s.WorkingDirectory = '%TARGET_DIR%'; $s.Description = 'Sonar Auto Switcher'; $s.Save()"

echo [5/5] Starting SonarAutoSwitch...
start "" "%TARGET_DIR%\SonarAutoSwitch.exe"

echo.
echo ========================================================
echo   [SUCCESS] SonarAutoSwitch is installed and active!
echo   Look for the headphone icon in your Windows Taskbar
echo   tray (next to the clock) to open Settings.
echo ========================================================
echo.
pause
