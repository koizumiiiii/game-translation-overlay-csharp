# ゲーム翻訳オーバーレイ - 開発者ドキュメント

## 目次

1. [アーキテクチャ概要](#アーキテクチャ概要)
2. [開発環境の構築](#開発環境の構築)
3. [コードの構造と設計](#コードの構造と設計)
4. [主要コンポーネント](#主要コンポーネント)
5. [診断モジュール](#診断モジュール)
6. [ビルドと展開](#ビルドと展開)
7. [テスト方法](#テスト方法)
8. [新機能の追加](#新機能の追加)
9. [トラブルシューティング](#トラブルシューティング)
10. [コントリビューションガイドライン](#コントリビューションガイドライン)

## アーキテクチャ概要

ゲーム翻訳オーバーレイは、OCR技術と機械翻訳を組み合わせたC#/.NETアプリケーションです。主に以下のコンポーネントから構成されています：

### 高レベルアーキテクチャ

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│                 │     │                 │     │                 │
│    OCRエンジン   │──→ │   翻訳エンジン   │──→ │  UIコンポーネント │
│                 │     │                 │     │                 │
└─────────────────┘     └─────────────────┘     └─────────────────┘
         ↑                       ↑                       ↑
         │                       │                       │
         └───────────────────────┴───────────────────────┘
                               │
                     ┌─────────────────────┐
                     │                     │
                     │  コアユーティリティ  │
                     │                     │
                     └─────────────────────┘
```

### レイヤードアーキテクチャ

アプリケーションは4つの主要レイヤーから構成されています：

1. **UIレイヤー** (Forms/): ユーザーインターフェース
2. **ビジネスロジックレイヤー** (Core/): アプリケーションのコア機能
3. **サービスレイヤー** (Services/): 外部サービスとの連携
4. **ユーティリティレイヤー** (Utils/): 共通ユーティリティ

各レイヤーは明確な責任を持ち、下位レイヤーのみに依存します。

## 開発環境の構築

### 必要な環境

- Windows 10/11
- Visual Studio 2022 (Community Edition以上)
- .NET Framework 4.8 SDK
- Git

### 手順

1. **リポジトリのクローン**

```bash
git clone https://github.com/example/game-translation-overlay.git
cd game-translation-overlay
```

2. **依存関係のインストール**

- PaddleOCRSharpをNuGetからインストール
- 翻訳サービスのビルド

```bash
cd tools/translation-service-builder
pip install -r requirements.txt
python build_standalone.py
```

3. **ソリューションのビルド**

Visual Studioでソリューションを開き、「ビルド」→「ソリューションのビルド」を選択します。

4. **初期設定**

`app/config.sample.json`を`app/config.json`にコピーし、必要に応じて設定を変更します。

## コードの構造と設計

### ディレクトリ構造

```
game-translation-overlay/
├── app/                        # メインアプリケーション
│   ├── Core/                   # コアロジック層
│   │   ├── Configuration/      # 設定管理
│   │   ├── Diagnostics/        # 診断機能
│   │   ├── Licensing/          # ライセンス管理
│   │   ├── OCR/                # OCR機能
│   │   ├── Region/             # 画面領域管理
│   │   ├── Security/           # セキュリティ機能
│   │   ├── Translation/        # 翻訳機能
│   │   └── Utils/              # 共通ユーティリティ
│   ├── Forms/                  # UI層
│   │   ├── MainForm/           # メインウィンドウ
│   │   └── OverlayForm/        # オーバーレイUI
│   ├── translation-service/    # 内蔵翻訳サービス
│   └── Resources/              # アプリケーションリソース
├── tests/                      # テストプロジェクト
│   └── GameTranslationOverlay.Tests/
├── tools/                      # 開発ツール
│   ├── translation-service-builder/  # 翻訳サービスビルドツール
│   └── key-encryption-tool/    # APIキー暗号化ツール
└── docs/                       # ドキュメント
```

### 設計原則

アプリケーションは以下の設計原則に基づいています：

1. **単一責任の原則 (SRP)**: 各クラスは単一の責任を持つ
2. **インターフェース分離の原則 (ISP)**: 具体的な実装はインターフェースを通じて提供
3. **依存性逆転の原則 (DIP)**: 高レベルのモジュールは低レベルのモジュールに依存しない
4. **コマンドクエリ分離の原則 (CQRS)**: 状態を変更するコマンドと、情報を取得するクエリを分離

## 主要コンポーネント

### OCRエンジン

OCRエンジンはPaddleOCRをベースに実装されています。

#### 重要なクラス

- **IOcrEngine**: OCRエンジンのインターフェース
- **PaddleOcrEngine**: PaddleOCRの実装
- **TextRegion**: 検出されたテキスト領域を表すモデル
- **OcrManager**: OCRエンジンの管理と調整

```csharp
public interface IOcrEngine
{
    Task InitializeAsync();
    Task<string> RecognizeTextAsync(Rectangle region);
    Task<List<TextRegion>> DetectTextRegionsAsync(Bitmap image);
}
```

#### テキスト検出プロセス

1. **画像キャプチャ**: 指定された領域からスクリーンショットを取得
2. **前処理**: 画像のコントラスト調整やノイズ除去
3. **テキスト検出**: OCRエンジンによるテキストの検出
4. **後処理**: 検出結果のフィルタリングと整形

### 翻訳エンジン

翻訳エンジンは複数の実装をサポートするプラグイン形式で設計されています。

#### 重要なクラス

- **ITranslationEngine**: 翻訳エンジンのインターフェース
- **LibreTranslateEngine**: ローカル翻訳の実装
- **AITranslationEngine**: AI翻訳の実装
- **TranslationManager**: 翻訳エンジンの管理と調整
- **TranslationCache**: 翻訳結果のキャッシュ

```csharp
public interface ITranslationEngine
{
    Task<string> TranslateAsync(string text, string fromLang, string toLang);
    bool IsAvailable { get; }
    IEnumerable<string> SupportedLanguages { get; }
}
```

#### 翻訳プロセス

1. **翻訳前の処理**: テキストの正規化と準備
2. **キャッシュチェック**: 既に翻訳したテキストかチェック
3. **言語検出**: 必要に応じてテキストの言語を自動検出
4. **翻訳実行**: 適切な翻訳エンジンによる翻訳
5. **キャッシュ保存**: 翻訳結果をキャッシュに保存

### ローカル翻訳サービス

スタンドアロンのLibreTranslateサービスを制御するコンポーネントです。

#### 重要なクラス

- **LocalTranslationService**: 翻訳サービスの制御
- **ServiceManager**: サービスの起動・停止・監視

```csharp
public class LocalTranslationService : IDisposable
{
    public async Task<bool> StartAsync();
    public void Stop();
    public bool IsRunning { get; }
    public void Dispose();
}
```

#### サービス管理プロセス

1. **サービス初期化**: アプリケーション起動時にサービスを初期化
2. **サービス起動**: 内蔵LibreTranslate実行ファイルをプロセスとして起動
3. **状態監視**: サービスの状態を監視し、必要に応じて再起動
4. **サービス停止**: アプリケーション終了時にサービスを停止

### UI層

UI層はWindows Formsで実装されています。

#### 重要なクラス

- **MainForm**: メインアプリケーションウィンドウ
- **OverlayForm**: ゲーム画面上に表示するオーバーレイウィンドウ
- **TranslationBox**: 翻訳結果を表示するコンポーネント
- **SettingsForm**: アプリケーション設定画面

#### UI設計の原則

- **シンプル**: 必要最小限のUIエレメントに抑える
- **非侵入的**: ゲームプレイを妨げないデザイン
- **カスタマイズ可能**: ユーザーが好みに合わせて調整可能
- **レスポンシブ**: バックグラウンド処理中もUIが応答する

### 設定と構成

アプリケーションの設定は`AppSettings`クラスで管理されています。

#### 重要なクラス

- **AppSettings**: アプリケーション設定の管理
- **LicenseManager**: ライセンスの管理
- **ApiKeyProtector**: APIキーの暗号化と保護

```csharp
public class AppSettings
{
    // シングルトンインスタンス
    public static AppSettings Instance { get; }
    
    // 設定プロパティ
    public string AppVersion { get; set; }
    public bool DebugModeEnabled { get; set; }
    public string TargetLanguage { get; set; }
    public bool UseAutoDetect { get; set; }
    public bool UseAITranslation { get; set; }
    public string LicenseKey { get; set; }
    
    // 設定の読み込みと保存
    public void LoadSettings();
    public void SaveSettings();
}
```

## 診断モジュール

診断モジュールは、アプリケーションの動作を監視、記録、および問題発生時の回復を支援するための一連のコンポーネントです。これらのコンポーネントは `GameTranslationOverlay.Core.Diagnostics` 名前空間に実装されています。

### コンポーネント構成

診断モジュールは以下の4つの主要コンポーネントで構成されています：

1. **Logger** - ログ記録機能
2. **ErrorReporter** - エラー報告機能
3. **DiagnosticsCollector** - システム・アプリケーション情報収集
4. **RecoveryManager** - エラー回復と自動修復機能

### 使用方法

#### 初期化

診断モジュールは以下のように初期化します：

```csharp
// Program.cs または適切な初期化場所で
string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameTranslationOverlay", "logs");
string errorReportDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameTranslationOverlay", "error_reports");
string recoveryLogPath = Path.Combine(logDirectory, "recovery.log");

// ロガーの初期化
Logger.Instance.Initialize(logDirectory);

// エラーレポーターの初期化
ErrorReporter.Instance.Initialize(errorReportDirectory);

// 回復マネージャーの初期化
RecoveryManager.Instance.Initialize(recoveryLogPath);
RecoveryManager.Instance.RegisterStandardRecoveryActions();
```

#### ログ記録

アプリケーション内でのログ記録は以下のように行います：

```csharp
// 情報ログ
Logger.Instance.Info("ClassName", "処理が正常に完了しました");

// 警告ログ
Logger.Instance.Warning("ClassName", "予期しない状態が発生しましたが、処理は継続します");

// エラーログ
try {
    // 何らかの処理
}
catch (Exception ex) {
    Logger.Instance.Error("ClassName", "処理中にエラーが発生しました", ex);
}

// 致命的エラーログ
Logger.Instance.Fatal("ClassName", "回復不能なエラーが発生しました", exception);
```

#### エラー報告

エラーを報告してレポートファイルを生成する方法：

```csharp
try {
    // 何らかの処理
}
catch (Exception ex) {
    // エラーをレポートしてファイルを生成
    string reportPath = ErrorReporter.Instance.ReportException(ex, "ClassName", "追加情報", true);
    
    // reportPathにはエラーレポートのファイルパスが含まれます
}
```

#### 診断情報の収集

システムおよびアプリケーションの診断情報を収集するには：

```csharp
// システム情報の収集
Dictionary<string, string> systemInfo = DiagnosticsCollector.Instance.CollectSystemInfo();

// アプリケーション情報の収集
Dictionary<string, string> appInfo = DiagnosticsCollector.Instance.CollectApplicationInfo();

// パフォーマンス指標の収集
Dictionary<string, string> performanceMetrics = DiagnosticsCollector.Instance.CollectPerformanceMetrics();

// 診断スナップショットの作成と保存
string snapshotPath = Path.Combine(reportDirectory, "diagnostic_snapshot.txt");
DiagnosticsCollector.Instance.SaveDiagnosticSnapshot(snapshotPath);
```

#### エラー回復と自動修復

エラー追跡と回復アクションの登録：

```csharp
// エラーを記録
bool actionTaken = RecoveryManager.Instance.RecordError("ComponentName", exception);

// カスタム回復アクションの登録
RecoveryManager.Instance.RegisterRecoveryAction(
    "データベース接続リセット",
    ex => ex is System.Data.SqlClient.SqlException,
    ex => {
        // データベース接続をリセットするコード
        Database.ResetConnection();
    }
);

// セーフモードの手動切り替え
if (criticalError) {
    RecoveryManager.Instance.EnableSafeMode("ManualTrigger", "管理者によるセーフモード有効化");
}

// セーフモードを無効化
RecoveryManager.Instance.DisableSafeMode();

// セーフモード変更の監視
RecoveryManager.Instance.SafeModeEnabled += (sender, args) => {
    // セーフモードが有効になった時の処理
    UpdateUI();
    ShowNotification($"セーフモードが有効になりました: {args.Reason}");
};
```

### 設計と拡張

#### Logger

`Logger` クラスはシングルトンパターンを採用し、5つのログレベル（Debug、Info、Warning、Error、Fatal）をサポートしています。ログはファイルに出力され、自動的にローテーションされます。

拡張ポイント：
- ログレベルのカスタマイズ
- ファイルローテーションのポリシー変更
- 出力先の追加（DB、クラウドなど）

#### ErrorReporter

`ErrorReporter` クラスはアプリケーションのクラッシュや例外を詳細なレポートとして保存します。未処理の例外をグローバルに捕捉し、分析用の情報を収集します。

拡張ポイント：
- カスタムエラーハンドラの追加
- レポート送信機能の追加
- エラーの重要度分類の実装

#### DiagnosticsCollector

`DiagnosticsCollector` クラスはシステム情報、アプリケーション情報、パフォーマンス指標を収集します。これらの情報はトラブルシューティングと問題分析に役立ちます。

拡張ポイント：
- 収集する情報の拡張
- 定期的な収集とトレンド分析
- カスタムパフォーマンスカウンターの追加

#### RecoveryManager

`RecoveryManager` クラスはエラーを追跡し、継続的なエラーが発生した場合に自動的に回復アクションを実行またはセーフモードに切り替えます。

拡張ポイント：
- カスタム回復アクションの追加
- セーフモード時の機能制限のカスタマイズ
- エラー閾値やタイムウィンドウの調整

### 設定オプション

診断モジュールは以下の設定オプションをサポートしています：

#### Logger設定

- **MinimumLogLevel**: 記録するログの最小レベル（デフォルト: Info）
- **MaxLogFileSize**: ログファイルの最大サイズ（デフォルト: 5MB）
- **MaxLogFiles**: 保持するログファイルの最大数（デフォルト: 5）

#### ErrorReporter設定

- **ShowErrorDialog**: エラー発生時にダイアログを表示するかどうか

#### RecoveryManager設定

- エラー閾値と時間枠の設定（`RecordError`メソッドのパラメータ）

### ファイル場所

診断モジュールで生成されるファイルは以下の場所に保存されます：

- ログファイル: `%LocalAppData%\GameTranslationOverlay\logs\`
- エラーレポート: `%LocalAppData%\GameTranslationOverlay\error_reports\`
- 回復ログ: `%LocalAppData%\GameTranslationOverlay\logs\recovery.log`

## ビルドと展開

### ビルド構成

- **Debug**: 開発とデバッグ用
- **Release**: 配布用
- **Portable**: ポータブル版（インストール不要）

### ビルドプロセス

1. **バージョン番号の更新**: `AssemblyInfo.cs`のバージョン情報を更新
2. **リソースの生成**: 翻訳リソースとアイコンの生成
3. **内蔵サービスのビルド**: LibreTranslateのスタンドアロン版をビルド
4. **アプリケーションのビルド**: Release構成でビルド
5. **インストーラーの作成**: NSISまたはWiXを使用してインストーラーを作成

### パッケージングとリリース

```bash
# リリースバージョンのビルド
msbuild GameTranslationOverlay.sln /p:Configuration=Release

# 翻訳サービスのビルド
cd tools/translation-service-builder
python build_standalone.py --output ../../app/bin/Release/translation-service

# インストーラーの作成
cd installer
makensis installer_script.nsi
```

### 継続的インテグレーション

GitHub Actionsを使用して、CI/CDパイプラインを実装しています：

- **Build**: プルリクエスト時にビルドとテストを実行
- **Release**: リリースタグ作成時に自動ビルドとリリース作成
- **Documentation**: ドキュメント更新時に自動デプロイ

## テスト方法

### 単体テスト

MSTestを使用して単体テストを実装しています。

```bash
# 単体テストの実行
cd tests
dotnet test GameTranslationOverlay.Tests
```

### 統合テスト

統合テストは特定のゲームやアプリケーションを使用して手動で実施します。

#### テストシナリオ

1. **OCR精度テスト**: 様々なゲームフォントでのOCR精度を検証
2. **翻訳品質テスト**: 翻訳結果の品質を評価
3. **パフォーマンステスト**: メモリ使用量とCPU負荷を監視
4. **長時間動作テスト**: 長時間実行時の安定性を確認

### パフォーマンスプロファイリング

Visual StudioのプロファイラーまたはDotMemoryを使用してパフォーマンス分析を行います。

#### 主なパフォーマンス指標

- **メモリ使用量**: 使用メモリが時間とともに増加しないことを確認
- **CPU使用率**: アイドル時のCPU使用率が5%以下であることを確認
- **レスポンス時間**: テキスト検出から翻訳表示までの時間が500ms以下であることを確認

## 新機能の追加

### 新しい翻訳エンジンの追加

1. `ITranslationEngine`インターフェースを実装する新しいクラスを作成
2. `TranslationManager`に新しいエンジンを登録
3. UI設定に新しいエンジンのオプションを追加

### 新しいOCRエンジンの追加

1. `IOcrEngine`インターフェースを実装する新しいクラスを作成
2. `OcrManager`に新しいエンジンを登録
3. UI設定に新しいエンジンのオプションを追加

### 機能の拡張

以下の一般的なプロセスに従ってください：

1. **設計**: 機能の詳細設計書を作成
2. **実装**: コードの実装とユニットテスト
3. **テスト**: 手動テストと統合テスト
4. **ドキュメント**: 開発者およびユーザードキュメントの更新
5. **プルリクエスト**: レビュー用にプルリクエストを作成

## トラブルシューティング

### 一般的な開発上の問題

1. **OCRエラー**: PaddleOCRのモデルファイルが正しい場所にあることを確認
2. **翻訳サービスエラー**: LibreTranslateのビルドが正しく行われていることを確認
3. **リソースエラー**: リソースファイルがプロジェクトに含まれていることを確認

### デバッグのヒント

- **デバッグモード**: `AppSettings.DebugModeEnabled = true`を設定して詳細なログを有効化
- **ログ出力**: `Debug.WriteLine`の出力をVisual Studioの出力ウィンドウで確認
- **例外処理**: 例外の詳細情報を`Debug.WriteLine`で出力
