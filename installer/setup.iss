; 行程簿 TripBook · 安装程序（Inno Setup 6）
; 页面结构与选项由 common.iss 统一提供；这里只声明应用身份与要安装的文件。
; 构建：installer\build-setup.cmd   产物：dist\TripBook-Setup-1.1.0.exe

#define AppName "行程簿"
#define AppSlug "TripBook"
#define DirName "TripBook"
#define AppVersion "1.1.0"
#define AppId "{{4B9D2E36-7CA5-4A4B-BF23-5DA08C6B9E47}"
#define AppExe "TripBook.exe"
#define AppRepo "TripBook"
#define AppTagline "把随手记的公交 / 地铁运转记录整理成时间轴，纯本地存储、不联网。"
#define IconPath "icon.ico"

#include "common.iss"

[Files]
Source: "{#AppRoot}\dist\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#AppRoot}\README.md"; DestDir: "{app}"
Source: "{#AppRoot}\LICENSE"; DestDir: "{app}"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: startmenuicon
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

