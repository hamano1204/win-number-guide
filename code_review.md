# WinNumberGuide コードレビューレポート

全体的に、WPFを用いたオーバーレイUI、低レベルキーボードフックによる入力監視、UI Automationを利用したタスクバー解析など、Windowsのシステム深くに関わる機能が綺麗にモジュール化されており、よく設計されたコードベースです。
一方で、UIのレスポンス（パフォーマンス）とリソース管理の観点でいくつか改善の余地（技術的負債）が見受けられます。

以下に詳細なレビュー結果をまとめます。

---

## 1. 致命的な課題（パフォーマンスとUIフリーズ）

### 1.1. 同期的な重い処理によるUIブロック
**対象ファイル**: `MainWindow.xaml.cs` (Hook_WinKeyLongPressed), `TaskbarReader.cs`, `IconExtractor.cs`

**問題点**:
Winキーを長押しした際、UIスレッド上で `TaskbarReader.GetTaskbarApps()` が同期的に呼び出されています。
このメソッド内部では各アプリのアイコンを取得するため、`Process.GetProcesses()` や `PackageManager.FindPackagesForUser()` といった**非常に重いOSレベルのAPIがループ内で何度も呼び出されます**。
これにより、Winキーを長押ししてからUIが表示されるまでに明らかなタイムラグ（UIのフリーズ）が発生する可能性が高いです。

**改善案**:
- **非同期化**: `GetTaskbarApps` を非同期メソッド（`Task<List<TaskbarApp>>`）にし、UIスレッドをブロックしないようにする。
- **アイコンの遅延読み込み**: アプリのリスト（名前とAppId）だけを先に取得してUIを表示し、アイコン画像はバックグラウンドで取得して完了次第バインド（反映）する仕組みにする。

### 1.2. 冗長なプロセス列挙とパッケージ検索
**対象ファイル**: `IconExtractor.cs`, `PackagedAppIconExtractor.cs`

**問題点**:
タスクバーのアプリ1つにつきアイコンを取得する際、毎回 `Process.GetProcesses()` を呼び出しています（最大10回）。プロセスの一覧取得は重い処理です。
また、UWPアプリのアイコン取得に使う `PackageManager.FindPackagesForUser` も非常に遅いAPIですが、これも呼ばれるたびに実行されています。

**改善案**:
- **キャッシュの導入**: 一度取得したアイコン（`AppId` と `ImageSource` のペア）は静的な `Dictionary` 等にキャッシュし、2回目以降はキャッシュから返すようにする。（アプリアイコンは起動中に変わることはほぼありません）。
- **プロセスのスナップショット**: どうしてもプロセス一覧が必要な場合は、ループの外で1回だけ `Process.GetProcesses()` を呼び出し、その配列を各検索メソッドに渡すようにする。

---

## 2. アーキテクチャと設計の改善点

### 2.1. 例外の握り潰し（Empty Catch Blocks）
**対象ファイル**: `IconExtractor.cs`, `PackagedAppIconExtractor.cs`, `MainWindow.xaml.cs`

**問題点**:
多くの場所で `catch { }` と記述され、例外が完全に無視されています。ヒューリスティックに情報をかき集める性質上、ある箇所が失敗しても次に進むという意図は理解できますが、開発時のデバッグが非常に困難になります。

**改善案**:
- 少なくとも `Debug.WriteLine(ex)` などを出力し、開発時に何が失敗したのか追跡できるようにする。

### 2.2. 未使用のP/Invokeやリソースリークの懸念
**対象ファイル**: `IconExtractor.cs`, `KeyboardHook.cs`

**問題点**:
- `IconExtractor.cs` で `DestroyIcon` や `ExtractIcon` といったネイティブAPIがインポートされていますが、使われていないか、正しく解放処理が記述されていない箇所があります。（例: `GetIconFromWindow` で取得した `hIcon` は解放が必要な場合があります）。
- `KeyboardHook.cs` の `_proc = HookCallback;` はガベージコレクション対策として正しく実装されていますが、全体的にアンマネージドリソースを扱うクラスなので注意が必要です。

**改善案**:
- 使っていないP/Invoke宣言は削除する。
- GDIリソース（hIconやBitmap）のリークが発生しないよう、WPFの `Imaging.CreateBitmapSourceFromHIcon` を呼んだ後に、所有権のあるハンドルは `DestroyIcon` で解放するロジックを確実にする。

### 2.3. TaskbarReaderの文字列パースの脆さ
**対象ファイル**: `TaskbarReader.cs`

**問題点**:
`SanitizeAppName` にて、`" - 1 つの実行中ウィンドウ"` などのサフィックスを正規表現で除去しています。実用的ではありますが、Windowsのアップデートで文言が変わったり、別言語のOSで実行されたりすると機能しなくなります。

**改善案**:
- 現状の実装でも実用上は問題ありませんが、可能であれば Automation Element の別のプロパティ（実行中のウィンドウ数を含まない素のアプリ名を持つもの）を探すか、プロセスの `MainModule.FileVersionInfo.FileDescription` などを優先して名前として採用するアプローチも検討できます。

---

## 3. 総評

現状でも目的の機能は十分に達成できるコードです。
しかし、普段使いのユーティリティとして「押したら一瞬で表示される」という**レスポンスの良さ**が命となるツールですので、**「アイコン取得のキャッシュ化」**と**「処理の非同期化（バックグラウンド処理）」**は最優先で対応することを強く推奨します。
