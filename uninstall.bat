@echo off
setlocal
title SonarAutoSwitch - Uninstaller
echo ========================================================
echo          SonarAutoSwitch Uninstaller
echo ========================================================
echo.

set "TARGET_DIR=%LOCALAPPDATA%\SonarAutoSwitch"
set "STARTUP_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"
set "SHORTCUT=%STARTUP_DIR%\SonarAutoSwitch.lnk"

echo Stopping running processes...
taskkill /F /IM SonarAutoSwitch.exe >nul 2>&1

echo Removing startup shortcut...
if exist "%SHORTCUT%" del /F /Q "%SHORTCUT%"

echo.
echo Do you want to delete settings and logs? (Y/N)
set /p REMOVE_DATA="> "
if /i "%REMOVE_DATA%"=="Y" (
    if exist "%TARGET_DIR%" rmdir /S /Q "%TARGET_DIR%"
    echo Deleted %TARGET_DIR%
)

echo.
echo [DONE] SonarAutoSwitch has been removed.
pause
