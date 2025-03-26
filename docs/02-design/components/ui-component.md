# ゲーム翻訳オーバーレイアプリケーション - UIコンポーネント

## 1. 概要

UIコンポーネントは、ゲーム翻訳オーバーレイアプリケーションにおいてユーザーとの対話を担う重要な部分です。このコンポーネントは、ゲームプレイを妨げることなく、オリジナルテキストの近くに翻訳結果を表示し、アプリケーションの直感的な操作を可能にします。アプリケーションの「ユーザー没入体験の重視」という基本方針に沿って、最小限の存在感ながら高い機能性を提供するよう設計されています。

### 1.1 目的と役割

- ゲーム画面上に透明なオーバーレイとして翻訳テキストを表示する
- アプリケーションの操作と設定を可能にするインターフェースを提供する
- クリックスルー機能を実装し、ゲームプレイへの干渉を最小化する
- 翻訳プロセスの状態をユーザーに視覚的にフィードバックする
- 多様な画面サイズや解像度に適応する柔軟なレイアウトを実現する

### 1.2 主要機能

- **透過オーバーレイ**: ゲーム画面上に半透明のUIを表示
- **翻訳表示ボックス**: 検出テキストの翻訳を表示
- **コントロールパネル**: アプリケーションの操作と設定
- **ターゲットウィンドウ選択**: 翻訳対象ウィンドウの指定
- **視覚的フィードバック**: 処理状態の表示
- **カスタマイズオプション**: フォント、色、透明度などの設定
- **キーボードショートカット**: 素早いアクセスと操作

## 2. デザインコンセプト

### 2.1 基本方針

- **最小限の存在感**: ユーザーの注意をゲームに集中させるデザイン
- **直感的操作**: 複雑な操作を必要としないシンプルなインターフェース
- **一貫したデザイン言語**: 統一された視覚要素による明確さと統一感
- **適応型レイアウト**: 様々な画面サイズと解像度に対応するレスポンシブデザイン
- **視覚的フィードバック**: ユーザー操作や処理状態に対する明確なフィードバック
- **非侵襲的**: ゲームプレイを妨げない配置とインタラクション

### 2.2 カラースキーム

- **基本カラーパレット**:
  - 背景: #2C2C2C（70%透過）
  - テキスト: #FFFFFF
  - アクセント: #4F94EF
  - セカンダリアクセント: #8BC34A

- **状態表示カラー**:
  - 有効/オン: #4CAF50（緑）
  - 無効/オフ: #9E9E9E（グレー）
  - エラー: #F44336（赤）
  - 警告: #FFC107（黄）
  - 処理中: #2196F3（青）

### 2.3 タイポグラフィ

- **UIフォント**: LINE Seed Sans（10-12pt）
- **翻訳テキストフォント**:
  - 日本語: LINE Seed JP（12-16pt）
  - 英語: LINE Seed Sans（12-16pt）
  - その他: システムフォントまたはNoto Sans
- **フォントウェイト**:
  - タイトル: Semi-Bold（600）
  - ボタン/ラベル: Regular（400）
  - 翻訳テキスト: Regular（400）

### 2.4 アイコノグラフィ

- シンプルな線画スタイルのアイコン
- SVG形式での実装による高解像度対応
- 機能を直感的に表現するアイコンデザイン
- アクションボタンには視認性の高いアイコン
- 状態を示すインジケーターアイコン

## 3. UIコンポーネント構成

### 3.1 メインウィンドウ

メインウィンドウはアプリケーションの中心的なコントロールパネルとして機能し、翻訳設定の管理や対象ウィンドウの選択などの主要機能へのアクセスを提供します。

#### 3.1.1 レイアウト

- サイズ: 400x300ピクセル（リサイズ可能）
- 位置: 初期位置は画面右上、ドラッグで自由に移動可能
- 構成: タブベースのインターフェースと最小化/最大化/閉じるコントロール

#### 3.1.2 タブナビゲーション

- **メイン**: 基本的な翻訳操作と制御
- **設定**: 詳細設定と構成
- **プロファイル**: ゲーム別プロファイル管理
- **ヘルプ**: 使用方法とサポート情報

#### 3.1.3 メインタブの構成要素

- **ウィンドウ選択エリア**:
  - 「翻訳対象ウィンドウを選択」ボタン
  - 選択中のウィンドウタイトル表示
  - ウィンドウサムネイル（オプション）

- **翻訳制御エリア**:
  - 翻訳ON/OFFトグルスイッチ
  - AI翻訳切替チェックボックス
  - 言語選択コンボボックス（日本語→英語、英語→日本語など）
  - 自動言語検出チェックボックス

- **OCR最適化エリア**:
  - 「現在のゲームのOCR設定を最適化」ボタン
  - 最適化状態インジケーター

- **ステータス表示エリア**:
  - 処理状況メッセージ
  - エラーインジケーター
  - ヘルプへのリンク

### 3.2 オーバーレイウィンドウ

オーバーレイウィンドウはゲーム画面上に直接表示され、翻訳テキストの表示とクイックコントロールを提供します。

#### 3.2.1 コントロールパネル

- **最小化状態（通常時）**:
  - 画面端に小さな透過アイコンのみ表示
  - 透過度: 70-80%
  - クリックで操作パネルに展開
  - サイズ: 40x40ピクセル

- **展開状態（操作時）**:
  - コンパクトなパネル表示
  - 透過度: 50-60%
  - 使用後は自動的または手動で最小化
  - サイズ: 300x60ピクセル

#### 3.2.2 操作パネルの機能

- 翻訳ON/OFF切替
- AI翻訳モード切替
- 言語ペア切替
- 設定画面呼び出し
- パネル最小化/閉じる

#### 3.2.3 翻訳テキスト表示

- **位置**: 原文テキストの直下または近く（カスタマイズ可能）
- **背景**: 半透明（30-40%の不透明度）
- **テキスト色**: 白または明るい色（カスタマイズ可能）
- **フォントサイズ**: 12pt〜16pt（ユーザー調整可能）
- **枠線**: 薄い境界線により翻訳領域を明示
- **相互作用**: テキスト選択可能（コピー機能）

### 3.3 設定ウィンドウ

設定ウィンドウでは、アプリケーションの詳細設定を管理します。

#### 3.3.1 一般設定タブ

- **表示設定**:
  - 言語選択
  - 起動時の動作
  - 最小化時の挙動
  - ホットキー設定

- **ウィンドウ設定**:
  - 自動ウィンドウ追跡
  - フォアグラウンド/バックグラウンド動作
  - 複数ディスプレイの取り扱い

#### 3.3.2 OCR設定タブ

- **エンジン設定**:
  - 精度/速度バランス調整
  - 差分検出の有効/無効
  - スキャン間隔調整
  - デバッグモード

- **最適化設定**:
  - 自動最適化の有効/無効
  - AI支援最適化の使用可否
  - プリセット選択

#### 3.3.3 翻訳設定タブ

- **エンジン選択**:
  - ローカル翻訳/オンライン翻訳
  - AI翻訳モード（Proプラン）
  - 自動言語検出

- **表示設定**:
  - フォント選択
  - テキストカラー
  - 背景透明度
  - 表示位置調整

#### 3.3.4 プロファイル設定タブ

- **プロファイル管理**:
  - ゲームプロファイル一覧
  - 新規プロファイル作成
  - プロファイル編集/削除
  - インポート/エクスポート

### 3.4 ターゲットウィンドウ選択ツール

ユーザーが翻訳対象のウィンドウを選択するための専用ツールです。

#### 3.4.1 レイアウト

- モーダルウィンドウとして表示
- 実行中のウィンドウ一覧表示
- プレビューサムネイル
- 検索フィルタ

#### 3.4.2 選択方法

- リストからウィンドウを選択
- 「クロスヘア」ツールによるウィンドウドラッグ
- 最近使用したウィンドウのクイック選択
- 自動検出（人気ゲームを認識）

## 4. ユーザーインタラクション

### 4.1 キーボードショートカット

| 機能 | ショートカット | 説明 |
|------|--------------|------|
| メインウィンドウ表示/非表示 | Ctrl+Shift+M | メインコントロールウィンドウを表示/非表示 |
| 翻訳ON/OFF | Ctrl+Shift+T | 翻訳処理の有効/無効を切り替え |
| AI翻訳切替 | Ctrl+Shift+A | AI翻訳モードの切り替え |
| 設定画面表示 | Ctrl+Shift+S | 設定タブを開く |
| アプリケーション終了 | Alt+F4 | アプリケーションを終了 |
| オーバーレイ表示/非表示 | Ctrl+Shift+O | オーバーレイの表示/非表示を切り替え |
| 言語ペア切替 | Ctrl+Shift+L | 翻訳言語ペアを切り替え |

### 4.2 マウス操作

- **ドラッグ&ドロップ**: 
  - ウィンドウ位置の移動
  - 翻訳表示ボックスの位置調整

- **右クリック**:
  - コンテキストメニュー表示
  - 翻訳テキストのコピー
  - クイック設定アクセス

- **ホイールスクロール**:
  - 翻訳履歴のスクロール
  - 透明度の調整（+Ctrl）
  - フォントサイズの調整（+Shift）

- **ダブルクリック**:
  - ウィンドウの最大化/通常サイズ切替
  - 翻訳ボックスのリセット

### 4.3 タッチ操作（オプション）

- **タップ**: クリックと同等
- **スワイプ**: スクロールと同等
- **ピンチイン/アウト**: 翻訳テキストのサイズ変更
- **長押し**: 右クリックメニューと同等

### 4.4 クリックスルー機能

クリックスルー機能により、ユーザーはオーバーレイUI上でのクリックをゲーム画面に直接伝えることができます。

- **フルクリックスルー**: オーバーレイ全体がクリック可能（コントロールを除く）
- **パーシャルクリックスルー**: 特定の透明部分のみクリック可能
- **トグル式クリックスルー**: ホットキーによる一時的な切り替え

### 4.5 フォーカス管理

- **メインウィンドウ**: 常に最前面表示オプション
- **オーバーレイ**: 常に最前面表示（クリックスルー有効）
- **アプリケーション全体**: 常に最前面表示機能のグローバル設定

## 5. クラス設計

### 5.1 UIレイヤーの主要クラス

#### 5.1.1 メインフォームクラス

```csharp
/// <summary>
/// アプリケーションのメインウィンドウ
/// </summary>
public partial class MainForm : Form
{
    // コアサービス
    private readonly IOcrEngine _ocrEngine;
    private readonly ITranslationManager _translationManager;
    private readonly ITextDetectionService _textDetectionService;
    
    // UIコンポーネント
    private OverlayForm _overlayForm;
    private WindowSelectorForm _windowSelectorForm;
    
    // 状態管理
    private IntPtr _targetWindowHandle = IntPtr.Zero;
    private bool _isTranslationActive = false;
    
    // コンストラクタ
    public MainForm(IOcrEngine ocrEngine, 
                   ITranslationManager translationManager, 
                   ITextDetectionService textDetectionService)
    {
        _ocrEngine = ocrEngine;
        _translationManager = translationManager;
        _textDetectionService = textDetectionService;
        
        InitializeComponent();
        InitializeOverlayForm();
        RegisterEventHandlers();
    }
    
    // オーバーレイフォーム初期化
    private void InitializeOverlayForm()
    {
        _overlayForm = new OverlayForm(_translationManager);
        _overlayForm.Show();
    }
    
    // イベントハンドラ登録
    private void RegisterEventHandlers()
    {
        _textDetectionService.OnRegionsDetected += TextDetectionService_OnRegionsDetected;
        _textDetectionService.OnNoRegionsDetected += TextDetectionService_OnNoRegionsDetected;
    }
    
    // テキスト領域検出イベント処理
    private async void TextDetectionService_OnRegionsDetected(object sender, List<TextRegion> regions)
    {
        if (!_isTranslationActive)
            return;
            
        try
        {
            var translatedRegions = new List<TranslatedTextRegion>();
            
            foreach (var region in regions)
            {
                string translatedText = await _translationManager.TranslateWithAutoDetectAsync(region.Text);
                translatedRegions.Add(new TranslatedTextRegion
                {
                    OriginalRegion = region,
                    TranslatedText = translatedText
                });
            }
            
            // UIスレッドで更新
            this.BeginInvoke(new Action(() => 
            {
                UpdateStatusText($"{translatedRegions.Count}個のテキストを翻訳しました");
                _overlayForm.DisplayTranslatedRegions(translatedRegions);
            }));
        }
        catch (Exception ex)
        {
            this.BeginInvoke(new Action(() => 
            {
                UpdateStatusText($"翻訳エラー: {ex.Message}", isError: true);
            }));
        }
    }
    
    // その他のメソッド...
}
```

#### 5.1.2 オーバーレイフォームクラス

```csharp
/// <summary>
/// ゲーム画面上のオーバーレイウィンドウ
/// </summary>
public partial class OverlayForm : Form
{
    // 依存サービス
    private readonly ITranslationManager _translationManager;
    
    // 翻訳表示ボックスのリスト
    private readonly List<TranslationBox> _translationBoxes = new List<TranslationBox>();
    
    // 表示設定
    private float _opacity = 0.8f;
    private Font _translationFont = new Font("LINE Seed JP", 12);
    private Color _textColor = Color.White;
    private Color _backgroundColor = Color.FromArgb(128, 44, 44, 44);
    
    // 状態
    private bool _isControlPanelExpanded = false;
    private bool _isTranslationActive = true;
    
    // コンストラクタ
    public OverlayForm(ITranslationManager translationManager)
    {
        _translationManager = translationManager;
        
        InitializeComponent();
        ConfigureFormProperties();
        SetupControlPanel();
    }
    
    // フォームプロパティの設定
    private void ConfigureFormProperties()
    {
        // クリックスルーを有効化
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        TransparencyKey = Color.Fuchsia; // 完全透過の色
        BackColor = Color.Fuchsia;
        
        // イベントハンドラ
        MouseClick += OverlayForm_MouseClick;
        KeyDown += OverlayForm_KeyDown;
    }
    
    // コントロールパネルのセットアップ
    private void SetupControlPanel()
    {
        controlPanel.BackColor = Color.FromArgb(192, 44, 44, 44);
        controlPanel.Visible = _isControlPanelExpanded;
        
        btnToggleTranslation.BackColor = _isTranslationActive ? 
            Color.FromArgb(255, 76, 175, 80) : Color.FromArgb(255, 158, 158, 158);
    }
    
    // 翻訳テキストの表示
    public void DisplayTranslatedRegions(List<TranslatedTextRegion> regions)
    {
        if (!_isTranslationActive)
            return;
        
        ClearTranslationBoxes();
        
        foreach (var region in regions)
        {
            var translationBox = CreateTranslationBox(region);
            Controls.Add(translationBox);
            _translationBoxes.Add(translationBox);
        }
    }
    
    // 翻訳表示ボックスの作成
    private TranslationBox CreateTranslationBox(TranslatedTextRegion region)
    {
        var box = new TranslationBox
        {
            TranslatedText = region.TranslatedText,
            OriginalRegion = region.OriginalRegion,
            Font = _translationFont,
            ForeColor = _textColor,
            BackColor = _backgroundColor,
            Opacity = _opacity
        };
        
        // 位置設定（原文の下に配置）
        Rectangle originalBounds = GetScreenRelativeBounds(region.OriginalRegion.Bounds);
        box.Location = new Point(originalBounds.X, originalBounds.Bottom + 5);
        box.Size = new Size(originalBounds.Width, box.GetPreferredHeight());
        
        return box;
    }
    
    // その他のメソッド...
}
```

#### 5.1.3 翻訳ボックスコントロールクラス

```csharp
/// <summary>
/// 翻訳テキストを表示するカスタムコントロール
/// </summary>
public class TranslationBox : Control
{
    // プロパティ
    public string TranslatedText { get; set; }
    public TextRegion OriginalRegion { get; set; }
    public double Opacity { get; set; } = 0.8;
    
    // 内部状態
    private bool _isHovered = false;
    private Color _baseBackColor;
    
    // コンストラクタ
    public TranslationBox()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | 
                ControlStyles.ResizeRedraw | 
                ControlStyles.UserPaint, true);
                
        _baseBackColor = BackColor;
        
        // イベントハンドラ
        MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
        MouseLeave += (s, e) => { _isHovered = false; Invalidate(); };
    }
    
    // 推奨高さの計算
    public int GetPreferredHeight()
    {
        using (Graphics g = CreateGraphics())
        {
            SizeF textSize = g.MeasureString(TranslatedText, Font);
            return Math.Max((int)textSize.Height + 10, 30);
        }
    }
    
    // 描画処理
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        // 背景色（透明度込み）
        Color bgColor = Color.FromArgb(
            (int)(255 * Opacity), 
            _baseBackColor.R, 
            _baseBackColor.G, 
            _baseBackColor.B);
            
        using (SolidBrush brush = new SolidBrush(bgColor))
        {
            g.FillRoundedRectangle(brush, ClientRectangle, 4);
        }
        
        // 枠線（ホバー時のみ）
        if (_isHovered)
        {
            using (Pen pen = new Pen(Color.FromArgb(128, 255, 255, 255), 1))
            {
                g.DrawRoundedRectangle(pen, ClientRectangle, 4);
            }
        }
        
        // テキスト描画
        using (SolidBrush textBrush = new SolidBrush(ForeColor))
        {
            RectangleF textRect = new RectangleF(5, 5, Width - 10, Height - 10);
            g.DrawString(TranslatedText, Font, textBrush, textRect);
        }
    }
    
    // その他のメソッド...
}
```

#### 5.1.4 ウィンドウ選択フォームクラス

```csharp
/// <summary>
/// 翻訳対象ウィンドウの選択ツール
/// </summary>
public partial class WindowSelectorForm : Form
{
    // イベント
    public event EventHandler<IntPtr> WindowSelected;
    
    // ウィンドウ情報リスト
    private List<WindowInfo> _windows = new List<WindowInfo>();
    
    // コンストラクタ
    public WindowSelectorForm()
    {
        InitializeComponent();
        LoadWindowList();
    }
    
    // ウィンドウリストの読み込み
    private void LoadWindowList()
    {
        _windows.Clear();
        lstWindows.Items.Clear();
        
        // 実行中のウィンドウを列挙
        NativeMethods.EnumWindows((hwnd, lparam) =>
        {
            if (NativeMethods.IsWindowVisible(hwnd) && !string.IsNullOrEmpty(GetWindowTitle(hwnd)))
            {
                var info = new WindowInfo { Handle = hwnd, Title = GetWindowTitle(hwnd) };
                _windows.Add(info);
                lstWindows.Items.Add(info.Title);
            }
            return true;
        }, IntPtr.Zero);
    }
    
    // ウィンドウタイトルの取得
    private string GetWindowTitle(IntPtr hwnd)
    {
        int length = NativeMethods.GetWindowTextLength(hwnd);
        if (length == 0) return string.Empty;
        
        StringBuilder sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
    
    // ウィンドウの選択
    private void lstWindows_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (lstWindows.SelectedIndex >= 0 && lstWindows.SelectedIndex < _windows.Count)
        {
            var selected = _windows[lstWindows.SelectedIndex];
            picPreview.Image = CaptureWindow(selected.Handle);
            lblWindowInfo.Text = $"タイトル: {selected.Title}\nハンドル: {selected.Handle}";
        }
    }
    
    // 選択確定ボタンの処理
    private void btnSelect_Click(object sender, EventArgs e)
    {
        if (lstWindows.SelectedIndex >= 0 && lstWindows.SelectedIndex < _windows.Count)
        {
            var selected = _windows[lstWindows.SelectedIndex];
            WindowSelected?.Invoke(this, selected.Handle);
            DialogResult = DialogResult.OK;
        }
    }
    
    // ウィンドウ情報構造体
    private struct WindowInfo
    {
        public IntPtr Handle;
        public string Title;
    }
    
    // その他のメソッド...
}
```

### 5.2 UIとコア層の連携

UIレイヤーは、コアレイヤーのサービスとインターフェースを通じて連携します。

```csharp
public static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        
        // コアサービスのインスタンス化
        var ocrEngine = new PaddleOcrEngine();
        var translationManager = new TranslationManager(
            new LibreTranslateEngine(),
            new AITranslationEngine(ApiKeyProtector.Instance.GetApiKey("openai"))
        );
        var textDetectionService = new TextDetectionService(ocrEngine);
        
        // メインフォームへの依存注入
        var mainForm = new MainForm(ocrEngine, translationManager, textDetectionService);
        
        // アプリケーション実行
        Application.Run(mainForm);
    }
}
```

## 6. レスポンシブ設計とアクセシビリティ

### 6.1 レスポンシブ設計

UIコンポーネントは様々な画面サイズや解像度に対応できるよう設計されています。

#### 6.1.1 画面解像度対応

- **DPIスケーリング**: システムDPI設定に基づく自動スケーリング
- **フォントスケーリング**: DPI変更に対応するフォントサイズ調整
- **レイアウト調整**: 画面サイズに応じたレイアウト変更
- **最小サイズ保証**: 小さな画面でも操作可能な最小サイズの確保

#### 6.1.2 マルチモニター対応

- **プライマリモニター検出**: プライマリディスプレイの自動検出
- **ディスプレイ間移動**: モニター間でのウィンドウ移動対応
- **DPI差異の処理**: 異なるDPI設定のモニター間でのスケーリング調整
- **位置記憶**: 各モニターでの位置を記憶

### 6.2 アクセシビリティ対応

#### 6.2.1 視覚的アクセシビリティ

- **コントラスト比**: WCAG AAA基準（7:1）を満たすテキスト/背景のコントラスト
- **色覚異常対応**: 色だけに依存しない情報伝達
- **テキストサイズ調整**: ユーザーによるフォントサイズの変更
- **ハイコントラストモード**: システムのハイコントラスト設定に対応

#### 6.2.2 操作アクセシビリティ

- **キーボード操作**: すべての機能をキーボードのみで操作可能
- **フォーカス表示**: 現在フォーカスの当たっている要素の明示
- **エラー通知**: 視覚的・聴覚的な複数の通知方法
- **ツールチップ**: 機能説明のツールチップ

## 7. パフォーマンス最適化

### 7.1 UI描画の最適化

- **ダブルバッファリング**: UI描画のちらつき防止
- **レイヤー描画**: 複雑なUIのレイヤー分割による描画最適化
- **カスタム描画**: 効率的なカスタム描画ロジック
- **アニメーション最適化**: 軽量なアニメーションの実装

### 7.2 UIスレッドの考慮

- **バックグラウンド処理**: 重い処理のバックグラウンドスレッドへの移動
- **UI更新の最適化**: 必要最小限のUI更新による負荷軽減
- **非同期操作**: 応答性を維持するための非同期処理
- **タイマー処理の最適化**: 適切な間隔設定と条件付き実行

### 7.3 メモリ管理

- **リソース解放**: 不要なリソースの適切な解放
- **画像処理の最適化**: 効率的な画像処理と解放
- **コントロールのライフサイクル管理**: UIコントロールの適切な作成と破棄
- **メモリリーク防止**: 循環参照の回避と適切なイベント解除

## 8. UI実装上の技術的考慮点

### 8.1 クリックスルー実装

クリックスルー機能の実装には、ウィンドウスタイルとヒットテスト処理のカスタマイズが必要です。

```csharp
// ウィンドウスタイルの設定
protected override CreateParams CreateParams
{
    get
    {
        CreateParams cp = base.CreateParams;
        
        // クリックスルーを有効にするフラグ
        cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
        
        return cp;
    }
}

// マウスヒットテストの処理
protected override void WndProc(ref Message m)
{
    // WM_NCHITTEST メッセージを処理
    if (m.Msg == 0x0084) // WM_NCHITTEST
    {
        // クリックスルー領域の場合
        if (ShouldPassThrough(PointToClient(Cursor.Position)))
        {
            m.Result = (IntPtr)(-1); // HTTRANSPARENT
            return;
        }
    }
    
    base.WndProc(ref m);
}

// クリックスルーすべき領域かどうかを判定
private bool ShouldPassThrough(Point clientPoint)
{
    // コントロールパネル上の場合はクリックスルーしない
    if (controlPanel.Bounds.Contains(clientPoint))
    {
        return false;
    }
    
    // 翻訳ボックス上の場合はクリックスルーしない
    foreach (var box in _translationBoxes)
    {
        if (box.Bounds.Contains(clientPoint))
        {
            return false;
        }
    }
    
    // それ以外の領域はクリックスルー
    return true;
}
```

### 8.2 透明ウィンドウの実装

半透明のオーバーレイウィンドウを実装するためのアプローチです。

```csharp
// フォームの初期化時に設定
private void InitializeTransparentWindow()
{
    // 完全に透過する色を設定
    TransparencyKey = Color.Fuchsia;
    BackColor = Color.Fuchsia;
    
    // 他のプロパティ設定
    FormBorderStyle = FormBorderStyle.None;
    ShowInTaskbar = false;
    TopMost = true;
}

// 部分的に透明な領域の描画
protected override void OnPaint(PaintEventArgs e)
{
    base.OnPaint(e);
    Graphics g = e.Graphics;
    
    // 背景を透過色で塗りつぶし
    g.Clear(Color.Fuchsia);
    
    // 半透明の背景を持つ領域を描画
    using (SolidBrush brush = new SolidBrush(Color.FromArgb(128, 44, 44, 44)))
    {
        foreach (var region in _visibleRegions)
        {
            g.FillRoundedRectangle(brush, region, 4);
        }
    }
}
```

### 8.3 ウィンドウ追跡の実装

対象ウィンドウの移動に合わせてオーバーレイを追従させる機能の実装です。

```csharp
// ウィンドウ追跡タイマー
private readonly Timer _windowTrackingTimer = new Timer { Interval = 100 };
private Rectangle _lastWindowBounds = Rectangle.Empty;

// ウィンドウ追跡の開始
public void StartWindowTracking(IntPtr targetWindowHandle)
{
    _targetWindowHandle = targetWindowHandle;
    _lastWindowBounds = GetWindowBounds(targetWindowHandle);
    
    // オーバーレイの初期位置設定
    UpdateOverlayPosition(_lastWindowBounds);
    
    // 追跡開始
    _windowTrackingTimer.Tick += WindowTrackingTimer_Tick;
    _windowTrackingTimer.Start();
}

// タイマーイベント処理
private void WindowTrackingTimer_Tick(object sender, EventArgs e)
{
    if (_targetWindowHandle == IntPtr.Zero)
        return;
        
    // ウィンドウが存在するか確認
    if (!NativeMethods.IsWindow(_targetWindowHandle))
    {
        _windowTrackingTimer.Stop();
        UpdateStatusText("ターゲットウィンドウが閉じられました", isError: true);
        return;
    }
    
    // ウィンドウの現在位置を取得
    Rectangle currentBounds = GetWindowBounds(_targetWindowHandle);
    
    // 位置が変わっていれば更新
    if (currentBounds != _lastWindowBounds)
    {
        UpdateOverlayPosition(currentBounds);
        _lastWindowBounds = currentBounds;
    }
}

// ウィンドウ位置の取得
private Rectangle GetWindowBounds(IntPtr hwnd)
{
    NativeMethods.RECT rect;
    NativeMethods.GetWindowRect(hwnd, out rect);
    return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
}

// オーバーレイ位置の更新
private void UpdateOverlayPosition(Rectangle targetBounds)
{
    if (_overlayForm != null)
    {
        _overlayForm.BeginInvoke(new Action(() => 
        {
            _overlayForm.SetBounds(
                targetBounds.X, 
                targetBounds.Y, 
                targetBounds.Width, 
                targetBounds.Height);
        }));
    }
}
```

## 9. 現在の実装状況

### 9.1 実装済み機能

- [x] 基本的なメインウィンドウUI
- [x] 透明オーバーレイウィンドウの基本構造
- [x] 翻訳テキスト表示の基本機能
- [x] ターゲットウィンドウ選択ツール
- [x] クリックスルー機能
- [x] 翻訳結果の表示

### 9.2 開発中機能

- [ ] 完全なUIテーマとスタイル適用
- [ ] 設定画面の詳細実装
- [ ] プロファイル管理UI
- [ ] レスポンシブ対応の強化
- [ ] パフォーマンス最適化

### 9.3 将来の拡張計画

- [ ] カスタムテーマのサポート
- [ ] 翻訳履歴表示UI
- [ ] ドラッグ可能な翻訳ボックス
- [ ] 拡張プラグインUI対応
- [ ] マルチディスプレイ環境での詳細最適化

## 10. 関連ドキュメント

- [プロジェクト概要](../01-overview/project-overview.md) - アプリケーションの概要
- [システムアーキテクチャ](../02-design/system-architecture.md) - システム全体の設計
- [OCRコンポーネント](../02-design/components/ocr-component.md) - OCR機能の詳細設計
- [翻訳コンポーネント](../02-design/components/translation-component.md) - 翻訳機能の詳細設計
- [パフォーマンス最適化](../03-implementation/performance-optimization.md) - 最適化の詳細
