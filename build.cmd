@echo off
rem ============================================================
rem  StationVoice - build script
rem  1) Prefer the .NET SDK (dotnet build, see StationVoice.csproj)
rem  2) Fall back to the .NET Framework compiler shipped with Windows
rem  Output: StationVoice.exe (single portable file, no dependencies)
rem ============================================================
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if not errorlevel 1 goto use_dotnet
goto use_csc

:use_dotnet
echo [1/2] dotnet build -c Release ...
dotnet build -c Release -v m
if errorlevel 1 (
  echo [ERROR] dotnet build failed
  exit /b 1
)
copy /y "bin\Release\StationVoice.exe" "StationVoice.exe" >nul
if errorlevel 1 (
  echo [ERROR] copy failed
  exit /b 1
)
echo [2/2] Done -^> StationVoice.exe
for %%F in ("StationVoice.exe") do echo       %%~zF bytes
exit /b 0

:use_csc
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [ERROR] Neither .NET SDK nor .NET Framework 4.x compiler found.
  exit /b 1
)

echo [1/2] csc compile StationVoice.exe ...
"%CSC%" -nologo -target:winexe -optimize+ -platform:anycpu -codepage:65001 -warn:4 ^
  -out:"StationVoice.exe" -win32manifest:"app.manifest" ^
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:System.Xml.dll ^
  src\*.cs src\Audio\*.cs src\Synth\*.cs src\Cli\*.cs src\SelfTest\*.cs src\Ui\*.cs src\Core\*.cs
if errorlevel 1 (
  echo [ERROR] compile failed
  exit /b 1
)

echo [2/2] Done -^> StationVoice.exe
for %%F in ("StationVoice.exe") do echo       %%~zF bytes
exit /b 0
