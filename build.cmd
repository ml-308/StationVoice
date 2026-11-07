@echo off
rem ============================================================
rem  StationVoice - build script
rem  Uses the .NET Framework 4.x C# compiler shipped with Windows.
rem  No NuGet, no third-party DLLs, single portable exe.
rem ============================================================
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [ERROR] csc.exe not found - .NET Framework 4.x is required.
  exit /b 1
)

rem System.Speech lives in the reference assemblies, not in the compiler folder.
set "LIBOPT="
set "REF48=%ProgramFiles(x86)%\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8"
set "REF46=%ProgramFiles(x86)%\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2"
set "REF45=%ProgramFiles(x86)%\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5"
if exist "%REF48%\System.Speech.dll" set "LIBOPT=-lib:%REF48%"
if not exist "%REF48%\System.Speech.dll" if exist "%REF46%\System.Speech.dll" set "LIBOPT=-lib:%REF46%"
if not exist "%REF48%\System.Speech.dll" if not exist "%REF46%\System.Speech.dll" if exist "%REF45%\System.Speech.dll" set "LIBOPT=-lib:%REF45%"

echo [1/2] Compiling StationVoice.exe ...
"%CSC%" -nologo -target:winexe -optimize+ -platform:anycpu -codepage:65001 -warn:4 ^
  -out:"StationVoice.exe" %LIBOPT% ^
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll ^
  -r:System.Speech.dll -r:System.Xml.dll ^
  src\*.cs src\Audio\*.cs src\Synth\*.cs src\Cli\*.cs src\SelfTest\*.cs src\Ui\*.cs src\Core\*.cs
if errorlevel 1 (
  echo [ERROR] compile failed
  exit /b 1
)

echo [2/2] Done -^> StationVoice.exe
for %%F in ("StationVoice.exe") do echo       %%~zF bytes
endlocal
