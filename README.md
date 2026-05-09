# WinNumberGuide

[日本語](#japanese) | [English](#english)

---

<a name="japanese"></a>
## 日本語 (Japanese)

WinNumberGuideは、Windowsキーを長押しした際に、タスクバーの各アプリアイコンに対応する番号（Win+数字キー）を画面中央に表示するユーティリティツールです。

### 主な機能
- **番号ガイド表示**: Winキーを長押し（約0.6秒）すると、タスクバー上のアプリアイコンとそれに対応するショートカット番号をオーバーレイ表示します。
- **直感的なレイアウト**: キーボードの左右の手の位置を考慮し、5番目と6番目の間にマージンを設けています。
- **システムトレイ常駐**: バックグラウンドで動作し、トレイアイコンから終了や設定が可能です。
- **自動起動**: Windows起動時に自動的に実行する設定が可能です。
- **モダンなUI**: 滑らかなフェードイン・フェードアウトアニメーションを採用しています。

### 使い方
1. アプリケーションを起動します（システムトレイに常駐します）。
2. `Win` キーを長押しすると、画面中央に番号ガイドが表示されます。
3. ガイドを見ながら `Win + 数字` を入力することで、目的のアプリを素早く起動・切り替えできます。

### 動作環境
- Windows 10 / 11
- .NET 10.0 ランタイム

### 注意事項
- 本アプリはAIで作成されたアプリです。
- そのため、コードの品質には注意が必要です。
- 危険なコードは含まれていないはずですが、自己責任でご利用ください。

## 開発者向け情報

### ビルド環境
- **OS**: Windows 10/11
- **フレームワーク**: .NET 10.0
- **言語**: C# 13.0
- **UI**: WPF

### ビルド手順
```bash
git clone https://github.com/hamano1204/win-number-guide.git
cd win-number-guide
dotnet build
```


---

<a name="english"></a>
## English

WinNumberGuide is a utility tool that displays the corresponding numbers (Win + number keys) for each taskbar app icon in the center of the screen when the Windows key is held down.

### Key Features
- **Number Guide Display**: Holding the `Win` key (approx. 0.6s) overlays the taskbar app icons with their respective shortcut numbers.
- **Intuitive Layout**: Includes a margin between the 5th and 6th items to match natural hand placement on the keyboard.
- **System Tray Integration**: Runs in the background with a tray icon for easy access to settings and exit.
- **Auto-Startup**: Option to automatically run the application when Windows starts.
- **Modern UI**: Features smooth fade-in and fade-out animations.

### How to Use
1. Launch the application (it will reside in the system tray).
2. Hold down the `Win` key to see the number guide in the center of the screen.
3. Use the guide to quickly launch or switch apps using `Win + Number`.

### Requirements
- Windows 10 / 11
- .NET 10.0 Runtime

### Notes
- This application was developed using AI.
- Please be aware that code quality may require attention.
- While it should not contain any malicious code, please use it at your own risk.

## Developer Information

### Build Environment
- **OS**: Windows 10/11
- **Framework**: .NET 10.0
- **Language**: C# 13.0
- **UI**: WPF

### Build Instructions
```bash
git clone https://github.com/hamano1204/win-number-guide.git
cd win-number-guide
dotnet build
```

---

## License

CC0 1.0 Universal


# ダウンロード / download

[download](https://github.com/hamano1204/win-number-guide/releases)

# スクリーンショット / screenshot

![screenshot](image/screenshot.png)
