# ゲーム翻訳オーバーレイアプリケーション - システムアーキテクチャ

## 1. アーキテクチャ概要

ゲーム翻訳オーバーレイアプリケーションは、Windows環境で動作するデスクトップアプリケーションであり、ゲーム画面のテキストをリアルタイムに認識し、翻訳するための機能を提供します。本ドキュメントでは、そのシステムアーキテクチャの詳細を解説します。

### 1.1 全体アーキテクチャ図

```
+---------------------+     +-------------------+     +---------------------+
|                     |     |                   |     |                     |
|    UIレイヤー       | --> |   コアレイヤー    | --> |   外部サービス      |
|   (Forms)           |     |   (Core)          |     |   連携レイヤー      |
|                     |     |                   |     |                     |
+---------------------+     +-------------------+     +---------------------+
         |                          |                           |
         |                          |                           |
         v                          v                           v
+---------------------+     +-------------------+     +---------------------+
|                     |     |                   |     |                     |
|   コンフィグレーション| <-- |   診断・ログ     | <-- |   データストレージ   |
|   (Configuration)   |     |   (Diagnostics)   |     |   (Storage)         |
|                     |     |                   |     |                     |
+---------------------+     +-------------------+     +---------------------+
```

### 1.2 採用アーキテクチャパターン

- **レイヤードアーキテクチャ**: UI層、ビジネスロジック層、データアクセス層の明確な分離
- **依存性逆転の原則**: 上位モジュールが下位モジュールに依存しない設計
- **シングルトンパターン**: アプリケーション全体で共有される設定やマネージャー
- **ストラテジーパターン**: 翻訳エンジンやOCRエンジンの実装を交換可能に
- **オブザーバーパターン**: コンポーネント間の通知方法としてのイベント駆動設計

### 1.3 主要設計目標

- **モジュール性**: 機能ごとに独立したコンポーネント設計
- **拡張性**: 新機能や対応言語の追加が容易
- **パフォーマンス**: ゲームのフレームレートに影響を与えない軽量な処理
- **安定性**: 長時間使用でも安定した動作
- **ユーザー体験**: 直感的で操作が簡単なインターフェース

## 2. レイヤー構造の詳細

### 2.1 UIレイヤー (Forms)

ユーザーインターフェースを提供するレイヤーです。Windows Forms（.NET Framework 4.8）を使用し、ユーザー入力の処理、視覚的フィードバック、設定画面を担当します。

#### 2.1.1 主要コンポーネント

| コンポーネント | 説明 | 機能 |
|--------------|------|------|
| **MainForm** | メイン管理ウィンドウ | アプリケーション全体の制御、ターゲットウィンドウ選択 |
| **OverlayForm** | 透過オーバーレイ | ゲーム画面上に翻訳を表示 |
| **TranslationBox** | 翻訳テキスト表示 | 翻訳結果を表示するコンポーネント |
| **WindowSelectorForm** | ウィンドウ選択 | 翻訳対象ウィンドウの選択ダイアログ |
| **SettingsForm** | 設定画面 | アプリケーション設定の管理 |

#### 2.1.2 UI設計の特徴

- **クリックスルー機能**: オーバーレイが下のゲーム操作を妨げない
- **最小限の存在感**: ユーザーの注意をゲームに集中させるデザイン
- **直感的なUI**: 単純な操作で機能を利用可能
- **適応型レイアウト**: 様々な画面サイズと解像度に対応

#### 2.1.3 UI層とコア層の連携

UIレイヤーはIoC（Inversion of Control）パターンを採用し、コアレイヤーのサービスを注入して利用します。

```csharp
// UIとコアサービスの連携例
public partial class MainForm : Form
{
    private readonly IOcrEngine _ocrEngine;
    private readonly ITranslationManager _translationManager;
    
    public MainForm(IOcrEngine ocrEngine, ITranslationManager translationManager)
    {
        _ocrEngine = ocrEngine;
        _translationManager = translationManager;
        InitializeComponent();
    }
    
    // フォーム上の操作がコアサービスに委譲される
}
```

### 2.2 コアレイヤー (Core)

アプリケーションの中核機能を提供するレイヤーです。OCR処理、翻訳処理、画面領域管理などのサブシステムで構成されます。

#### 2.2.1 OCRサブシステム

光学文字認識（OCR）機能を提供します。

| コンポーネント | 説明 | 機能 |
|--------------|------|------|
| **OcrManager** | OCR処理の統合管理 | OCRエンジンの初期化・管理、処理の調整 |
| **PaddleOcrEngine** | OCRエンジン実装 | PaddleOCRを使用したテキスト認識 |
| **TextDetectionService** | テキスト検出サービス | 画面のテキスト検出と監視 |
| **DifferenceDetector** | 変更検出 | 画面変更の検出による処理最適化 |
| **OcrOptimizer** | 最適化機能 | AI支援によるOCR設定の最適化 |

**主要インターフェース**:

```csharp
public interface IOcrEngine : IDisposable
{
    Task InitializeAsync();
    Task<List<TextRegion>> DetectTextRegionsAsync(Bitmap image);
    Task<string> RecognizeTextAsync(Rectangle region);
    void SetConfidenceThreshold(float threshold);
    void SetPreprocessingOptions(PreprocessingOptions options);
}

public interface ITextDetectionService : IDisposable
{
    void Start();
    void Stop();
    void SetTargetWindow(IntPtr windowHandle);
    event EventHandler<List<TextRegion>> OnRegionsDetected;
    event EventHandler OnNoRegionsDetected;
}
```

#### 2.2.2 翻訳サブシステム

検出されたテキストの翻訳を担当します。

| コンポーネント | 説明 | 機能 |
|--------------|------|------|
| **TranslationManager** | 翻訳処理の一元管理 | 翻訳エンジンの選択と使用 |
| **LibreTranslateEngine** | ローカル翻訳 | ローカル翻訳サービスとの連携 |
| **AITranslationEngine** | AI翻訳 | AI翻訳サービス（OpenAI/Google）との連携 |
| **TranslationCache** | キャッシュ機能 | 翻訳結果のキャッシュによる最適化 |
| **LanguageDetector** | 言語検出 | テキストの言語自動検出 |

**主要インターフェース**:

```csharp
public interface ITranslationEngine : IDisposable
{
    Task<string> TranslateAsync(string text, string fromLang, string toLang);
    bool IsAvailable { get; }
    IEnumerable<string> SupportedLanguages { get; }
}

public interface ITranslationManager : IDisposable
{
    Task<string> TranslateAsync(string text, string fromLang, string toLang);
    Task<string> TranslateWithAutoDetectAsync(string text);
    void EnableAITranslation(bool enable);
}
```

#### 2.2.3 画面領域管理サブシステム

ゲーム画面の選択と管理を担当します。

| コンポーネント | 説明 | 機能 |
|--------------|------|------|
| **RegionManager** | 領域管理 | 画面領域の選択と追跡 |
| **ScreenCapture** | 画面キャプチャ | 画面内容のキャプチャ |
| **WindowManager** | ウィンドウ管理 | ウィンドウハンドルとプロパティの管理 |

**主要インターフェース**:

```csharp
public interface IRegionManager
{
    Rectangle GetActiveRegion();
    void SetActiveRegion(Rectangle region);
    List<Rectangle> GetPredefinedRegions();
}

public interface IScreenCapture
{
    Bitmap CaptureWindow(IntPtr hwnd);
    Bitmap CaptureRegion(Rectangle region);
}
```

#### 2.2.4 セキュリティサブシステム

APIキーの暗号化と管理を担当します。

| コンポーネント | 説明 | 機能 |
|--------------|------|------|
| **ApiKeyProtector** | APIキー保護 | APIキーの暗号化と管理 |
| **ApiMultiKeyProtector** | 複数APIキー管理 | 複数サービスのAPIキー管理 |
| **EncryptionHelper** | 暗号化機能 | 汎用暗号化機能 |

**主要インターフェース**:

```csharp
public interface IApiKeyProvider
{
    string GetApiKey(string provider, string keyId = "default");
    bool ValidateApiKeyFormat(string provider, string apiKey);
}
```

### 2.3 外部サービス連携レイヤー

外部サービスとの通信を担当するレイヤーです。

| コンポーネント | 説明 | 機能 |
|--------------|------|------|
| **VisionServiceClient** | AIビジョン連携 | OCR最適化のためのAIサービス連携 |
| **HttpClientWrapper** | HTTP通信 | HTTP通信の抽象化 |
| **LibreTranslateClient** | ローカル翻訳連携 | LibreTranslateサーバーとの通信 |

### 2.4 診断・ログレイヤー (Diagnostics)

アプリケーションの状態監視、エラー処理、パフォーマンス測定を担当します。

| コンポーネント | 説明 | 機能 |
|--------------|------|------|
| **Logger** | ログ記録 | アプリケーションログの記録 |
| **ErrorReporter** | エラー報告 | エラー情報の収集と報告 |
| **DiagnosticsCollector** | 診断情報 | パフォーマンスメトリクスの収集 |
| **RecoveryManager** | 回復機能 | エラー時の回復処理 |

### 2.5 コンフィグレーションレイヤー (Configuration)

アプリケーション設定の管理を担当します。

| コンポーネント | 説明 | 機能 |
|--------------|------|------|
| **AppSettings** | アプリ設定 | 全般設定の管理 |
| **GameProfiles** | ゲームプロファイル | ゲームごとの設定プロファイル |
| **LicenseManager** | ライセンス管理 | ライセンス状態の管理 |
| **SettingsConverter** | 設定変換 | 設定データの変換 |

## 3. 詳細なクラス依存関係

### 3.1 主要コンポーネント間の依存関係

```
MainForm
 ├── OcrManager
 │    └── PaddleOcrEngine
 ├── TranslationManager
 │    ├── LibreTranslateEngine
 │    └── AITranslationEngine
 ├── TextDetectionService
 │    └── DifferenceDetector
 └── OverlayForm
      └── TranslationBox

AppSettings
 └── GameProfiles

ApiKeyProtector
 └── EncryptionHelper
```

### 3.2 依存性注入の例

アプリケーションでは明示的な依存性注入を使用して、コンポーネント間の結合度を低減します。

```csharp
// Program.cs
static void Main()
{
    // サービスのインスタンス化
    var ocrEngine = new PaddleOcrEngine();
    var translationManager = new TranslationManager(
        new LibreTranslateEngine(),
        new AITranslationEngine(ApiKeyProtector.Instance.GetApiKey("openai"))
    );
    var textDetectionService = new TextDetectionService(ocrEngine);
    
    // メインフォームへの注入
    Application.Run(new MainForm(ocrEngine, translationManager, textDetectionService));
}
```

## 4. スレッド管理

### 4.1 UIスレッド

- Windows FormsのUI更新はUIスレッドで実行
- バックグラウンド処理からUIを更新する場合は`BeginInvoke`/`Invoke`を使用

```csharp
// UIスレッドを考慮した更新例
private void UpdateUI(string translatedText)
{
    if (InvokeRequired)
    {
        BeginInvoke(new Action<string>(UpdateUI), translatedText);
        return;
    }
    
    translationLabel.Text = translatedText;
}
```

### 4.2 バックグラウンド処理

- OCRと翻訳処理は非同期（async/await）で実行
- 画面キャプチャと分析は定期的な`Timer`イベントで実行
- リソース競合を防ぐための同期機構

```csharp
// TextDetectionService.cs
private async void DetectionTimer_Tick(object sender, EventArgs e)
{
    // タイマーの一時停止
    _detectionTimer.Stop();
    
    try
    {
        // 非同期OCR処理
        using (Bitmap screenshot = _screenCapture.CaptureWindow(_targetWindowHandle))
        {
            // 差分検出
            if (_differenceDetector.HasSignificantChange(screenshot))
            {
                // テキスト検出
                var regions = await _ocrEngine.DetectTextRegionsAsync(screenshot);
                
                // 検出結果通知
                if (regions.Count > 0)
                {
                    OnRegionsDetected?.Invoke(this, regions);
                }
                else
                {
                    OnNoRegionsDetected?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }
    catch (Exception ex)
    {
        Logger.Instance.LogError($"テキスト検出エラー: {ex.Message}");
    }
    finally
    {
        // タイマーの再開
        _detectionTimer.Start();
    }
}
```

## 5. エラー処理戦略

### 5.1 例外ハンドリング方針

- コアロジックでは適切な例外をスロー
- 上位レイヤーで例外をキャッチし、ユーザーフレンドリーなエラー表示
- 継続運用を可能にするためのエラー回復メカニズム

### 5.2 エラー回復メカニズム

1. OCRエンジンエラー時のフォールバック処理
2. 翻訳サービス接続失敗時の代替手段
3. 自己診断と自動リカバリー

```csharp
// TranslationManager.cs
public async Task<string> TranslateAsync(string text, string fromLang, string toLang)
{
    try
    {
        // キャッシュ確認
        string cached = _cache.GetTranslation(text, fromLang, toLang);
        if (cached != null)
            return cached;
            
        // プライマリ翻訳エンジンでの翻訳
        return await _primaryEngine.TranslateAsync(text, fromLang, toLang);
    }
    catch (Exception ex)
    {
        Logger.Instance.LogWarning($"プライマリ翻訳エンジンエラー: {ex.Message}");
        
        try
        {
            // フォールバックエンジンでの翻訳
            return await _fallbackEngine.TranslateAsync(text, fromLang, toLang);
        }
        catch (Exception fallbackEx)
        {
            Logger.Instance.LogError($"翻訳エラー: {fallbackEx.Message}");
            return $"[翻訳エラー] {text}";
        }
    }
}
```

## 6. パフォーマンス考慮事項

### 6.1 リソース使用の最適化

- **メモリ使用量**: 推奨上限300MB以内に抑制
  - 不要なリソースの積極的な解放
  - 大きな画像データの効率的な管理
  - メモリ使用量の定期的なモニタリングと自動調整

- **CPU使用率**: アイドル時5%以下、処理時20%以下に抑制
  - 不要な処理の延期・省略
  - 差分検出による処理の最小化
  - バックグラウンド優先度の調整

- **バッテリー消費**: モバイルデバイスでの使用を考慮
  - 省電力モードの実装
  - 画面更新頻度の動的調整
  - 不使用時の自動スリープ

### 6.2 レスポンス時間の最適化

- **テキスト検出〜翻訳表示**: 目標300ms以内
  - キャッシュの積極的活用
  - 予測的処理の実装
  - 低レベルAPIの使用による高速化

## 7. 拡張性と将来の展望

### 7.1 拡張ポイント

- **OCRエンジン**: 新しいOCRエンジンの追加が可能
- **翻訳エンジン**: 新しい翻訳サービスの統合が可能
- **UI**: 新しいコントロールや画面の追加が容易

### 7.2 将来の拡張候補

- **プラグインシステム**: サードパーティによる機能拡張
- **クラウド同期**: 設定やプロファイルの同期
- **拡張API連携**: より多くのAIサービスとの連携

### 7.3 代替実装戦略

- **代替OCRエンジン**: PaddleOCRが機能しない環境では代替エンジンを使用
- **代替翻訳エンジン**: 翻訳サービスの可用性に応じた切り替え

## 8. 実際の実装状況

### 8.1 現在の開発状態

| コンポーネント | 状態 | 備考 |
|--------------|------|------|
| OCR機能 | ほぼ完了 | PaddleOCR統合、差分検出実装済み |
| 翻訳機能 | 実装中 | ローカル翻訳完了、AI翻訳実装中 |
| オーバーレイUI | 実装中 | 基本機能動作、UX改善が必要 |
| 設定・構成 | 実装中 | ゲームプロファイル機能実装中 |
| セキュリティ | 実装中 | APIキー管理実装済み、ライセンス管理開発中 |
| パフォーマンス最適化 | 実装中 | 差分検出による最適化実装済み |

### 8.2 将来の実装計画

- AI支援によるOCR最適化の完成
- 多言語翻訳機能の拡充
- 翻訳精度向上のための文脈分析
- パフォーマンス最適化の継続

## 9. 関連ドキュメント

- [アーキテクチャ概要](../01-overview/architecture-summary.md) - アーキテクチャの概要説明
- [OCR機能設計](../02-design/components/ocr-component.md) - OCRコンポーネントの詳細設計
- [翻訳機能設計](../02-design/components/translation-component.md) - 翻訳コンポーネントの詳細設計
- [UI設計仕様](../02-design/components/ui-component.md) - UIコンポーネントの詳細設計
- [セキュリティ設計](../02-design/components/security-component.md) - セキュリティの詳細設計
- [データフロー設計](../02-design/data-flow.md) - 主要データフローの詳細
