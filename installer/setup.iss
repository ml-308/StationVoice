; 报站声色模仿器 StationVoice · 安装程序（Inno Setup 6）
; 页面结构与选项由 common.iss 统一提供；这里只声明应用身份与要安装的文件。
; 构建：installer\build-setup.cmd   产物：dist\StationVoice-Setup-1.2.0.exe

#define AppName "报站声色模仿器"
#define AppSlug "StationVoice"
#define DirName "StationVoice"
#define AppVersion "1.2.0"
#define AppId "{{6DBF4A58-9EC7-4C6D-D145-7FC20E8DB069}"
#define AppExe "StationVoice.exe"
#define AppRepo "StationVoice"
#define AppTagline "分析参考录音的声色，用系统语音批量生成公交 / 地铁报站音频（WAV / MP3 / M4A）。"
#define IconPath "app.ico"

#include "common.iss"

[Files]
Source: "{#AppRoot}\StationVoice.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppRoot}\app.ico"; DestDir: "{app}"
Source: "{#AppRoot}\README.md"; DestDir: "{app}"
Source: "{#AppRoot}\LICENSE"; DestDir: "{app}"
Source: "{#AppRoot}\RELEASE_NOTES.md"; DestDir: "{app}"
; 示例报站稿：首次安装给一份，用户改过的不会被覆盖
Source: "{#AppRoot}\templates\*.txt"; DestDir: "{app}\templates"; Flags: onlyifdoesntexist

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; IconFilename: "{app}\app.ico"; Tasks: startmenuicon
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon
