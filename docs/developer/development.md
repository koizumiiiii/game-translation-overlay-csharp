# ゲーム翻訳オーバーレイ - 開発者ドキュメント

## 目次

1. [アーキテクチャ概要](#アーキテクチャ概要)
2. [開発環境の構築](#開発環境の構築)
3. [コードの構造と設計](#コードの構造と設計)
4. [主要コンポーネント](#主要コンポーネント)
5. [ビルドと展開](#ビルドと展開)
6. [テスト方法](#テスト方法)
7. [新機能の追加](#新機能の追加)
8. [トラブルシューティング](#トラブルシューティング)
9. [コントリビューションガイドライン](#コントリビューションガイドライン)

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

### エラーログの解析

```csharp
// エラーログの例
try
{
    // 処理
}
catch (Exception ex)
{
    Debug.WriteLine($"[ERROR] {ex.GetType().Name}: {ex.Message}");
    Debug.WriteLine($"Stack