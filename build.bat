@echo off
setlocal
echo ==============================================
echo        Building SonarAutoSwitch...
echo ==============================================

if not exist bin mkdir bin

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo [ERROR] .NET Framework C# compiler csc.exe not found!
    exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize /out:bin\SonarAutoSwitch.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:Microsoft.CSharp.dll src\*.cs

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] Built successfully: bin\SonarAutoSwitch.exe
) else (
    echo [ERROR] Build failed!
    exit /b %ERRORLEVEL%
)
