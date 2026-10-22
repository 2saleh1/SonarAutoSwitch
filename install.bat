@echo off
setlocal
title SonarAutoSwitch - Installer
echo ========================================================
echo          SonarAutoSwitch 1-Click Installer
echo ========================================================
echo.

set "TARGET_DIR=%LOCALAPPDATA%\SonarAutoSwitch"
set "STARTUP_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"
set "STARTUP_SHORTCUT=%STARTUP_DIR%\SonarAutoSwitch.lnk"
set "DESKTOP_SHORTCUT=%USERPROFILE%\Desktop\SonarAutoSwitch.lnk"

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
copy /Y "%~dp0app.ico" "%TARGET_DIR%\app.ico" >nul 2>&1

echo [4/5] Setting up Shortcuts (Desktop & Startup)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ws = New-Object -ComObject WScript.Shell; $s1 = $ws.CreateShortcut('%STARTUP_SHORTCUT%'); $s1.TargetPath = '%TARGET_DIR%\SonarAutoSwitch.exe'; $s1.Arguments = '--silent'; $s1.WorkingDirectory = '%TARGET_DIR%'; $s1.IconLocation = '%TARGET_DIR%\SonarAutoSwitch.exe,0'; $s1.Description = 'Sonar Auto Switcher (Background)'; $s1.Save(); $s2 = $ws.CreateShortcut('%DESKTOP_SHORTCUT%'); $s2.TargetPath = '%TARGET_DIR%\SonarAutoSwitch.exe'; $s2.WorkingDirectory = '%TARGET_DIR%'; $s2.IconLocation = '%TARGET_DIR%\SonarAutoSwitch.exe,0'; $s2.Description = 'Sonar Auto Switcher'; $s2.Save()"

echo [5/5] Launching SonarAutoSwitch...
start "" "%TARGET_DIR%\SonarAutoSwitch.exe"

echo.
echo ========================================================
echo   [SUCCESS] SonarAutoSwitch is installed and active!
echo   Open the program anytime from the Desktop icon.
echo ========================================================
echo.
pause
