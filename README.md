# 🎯 Roblox Tracker

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D4?logo=windows&logoColor=white)](https://github.com/Anlu-Dev/RobloxTracker/releases)
[![Release](https://img.shields.io/github/v/release/Anlu-Dev/RobloxTracker?include_prereleases&label=release)](https://github.com/Anlu-Dev/RobloxTracker/releases)
[![Status](https://img.shields.io/badge/status-beta-F5A623)](https://github.com/Anlu-Dev/RobloxTracker/releases)

A modern, dark-themed Windows application for viewing public Roblox profile information.

Roblox Tracker is built with **C#**, **WPF**, and **.NET 8**. Search for a Roblox username to view account details, social statistics, groups, and other publicly available information.

> [!IMPORTANT]
> Roblox Tracker is an unofficial community project and is not affiliated with, endorsed by, or sponsored by Roblox Corporation.

## Features

- Account dashboard with avatar, display name, username, user ID, creation date, and account age
- Friends, followers, and following statistics
- Friend list with links to Roblox profiles
- Group membership viewer with roles and ranks
- Inventory viewer (**Beta**)
- Korean and English interface
- Remembered username and optional automatic connection
- Adjustable API request delay and safe API mode
- Self-contained Windows x64 release
- Modern dark interface with custom application icon

## Download and Run

1. Open the [Releases page](https://github.com/Anlu-Dev/RobloxTracker/releases).
2. Download `RobloxTracker-v1.7-beta-win-x64.zip`.
3. Extract the entire ZIP file.
4. Run `RobloxTracker.exe`.

Visual Studio and a separate .NET installation are **not required** for the self-contained release.

> [!NOTE]
> Windows SmartScreen may display an “Unknown publisher” warning because the application is not digitally signed.

## 한국어 사용 안내

1. [Releases](https://github.com/Anlu-Dev/RobloxTracker/releases)에서 `RobloxTracker-v1.7-beta-win-x64.zip`을 다운로드합니다.
2. ZIP 파일의 압축을 전부 풉니다.
3. `RobloxTracker.exe`를 실행합니다.
4. 앱의 **Settings → Language**에서 `한국어`를 선택할 수 있습니다.

배포 버전은 자체 포함 방식이므로 Visual Studio와 별도의 .NET 설치가 필요하지 않습니다.

## Requirements

- Windows 10 or Windows 11
- 64-bit Windows (`win-x64`)
- Internet connection

## Privacy and Safety

- Uses only publicly available Roblox profile information
- Does not request or store Roblox passwords
- Does not request `.ROBLOSECURITY` cookies or authentication tokens
- Opens profile, group, and catalog links in the default web browser
- Stores app preferences locally in `%LocalAppData%\RobloxTracker\settings.json`

Never enter your Roblox password or security cookie into unofficial applications.

## Known Limitations

- The Inventory tab is experimental and may be unavailable or incomplete.
- Roblox APIs may temporarily return `429 Too Many Requests`.
- Private inventory information cannot be displayed.
- API behavior may change without notice.

If requests are being limited, enable **Safe API Mode** in Settings and increase the request interval.

## Build From Source

### Prerequisites

- Visual Studio with the **.NET desktop development** workload, or
- .NET 8 SDK

### Build

```bash
git clone https://github.com/Anlu-Dev/RobloxTracker.git
cd RobloxTracker
dotnet restore RobloxTracker/RobloxTracker.csproj
dotnet build RobloxTracker/RobloxTracker.csproj -c Release
```

### Publish a Self-Contained Windows Build

```powershell
dotnet publish RobloxTracker/RobloxTracker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

WPF may keep several native Windows DLL files next to the executable. Distribute the complete publish folder rather than only the `.exe` file.

## Project Structure

```text
RobloxTracker/
├── RobloxTracker.slnx
├── .gitignore
└── RobloxTracker/
    ├── App.xaml
    ├── AppIcon.ico
    ├── AssemblyInfo.cs
    ├── MainWindow.xaml
    ├── MainWindow.xaml.cs
    └── RobloxTracker.csproj
```

## Feedback

Found a bug or have a feature idea? Open an [Issue](https://github.com/Anlu-Dev/RobloxTracker/issues).

When reporting an issue, include:

- What you expected to happen
- What actually happened
- Steps to reproduce the problem
- A screenshot or error message, if available

---

Made by [Anlu-Dev](https://github.com/Anlu-Dev)
