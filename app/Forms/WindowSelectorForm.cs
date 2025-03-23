using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using GameTranslationOverlay.Core.UI;
using GameTranslationOverlay.Core.Models;

namespace GameTranslationOverlay.Forms
{
    /// <summary>
    /// ウィンドウ選択ダイアログ
    /// </summary>
    public class WindowSelectorForm : Form
    {
        private ListView windowListView;
        private Button selectButton;
        private Button cancelButton;
        private ImageList thumbnailImageList;
        private Label instructionLabel;

        /// <summary>
        /// 選択されたウィンドウ情報
        /// </summary>
        public WindowInfo SelectedWindow { get; private set; }

        /// <summary>
        /// 選択されたウィンドウハンドル
        /// </summary>
        public IntPtr SelectedWindowHandle 
        { 
            get { return SelectedWindow?.Handle ?? IntPtr.Zero; } 
        }

        /// <summary>
        /// 選択されたウィンドウのタイトル
        /// </summary>
        public string SelectedWindowTitle
        {
            get { return SelectedWindow?.Title ?? string.Empty; }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public WindowSelectorForm()
        {
            InitializeComponents();
            LoadWindowList();
        }

        /// <summary>
        /// コンポーネントの初期化
        /// </summary>
        private void InitializeComponents()
        {
            this.Text = "翻訳対象ウィンドウの選択";
            this.Size = new Size(640, 520);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = SystemIcons.Application;

            // 説明ラベル
            instructionLabel = new Label
            {
                Text = "翻訳したいゲームやアプリケーションのウィンドウを選択してください。",
                Location = new Point(12, 12),
                Size = new Size(600, 20),
                AutoSize = false
            };

            // サムネイル用のImageList
            thumbnailImageList = new ImageList
            {
                ImageSize = new Size(160, 120),
                ColorDepth = ColorDepth.Depth32Bit
            };

            // ウィンドウリスト
            windowListView = new ListView
            {
                Location = new Point(12, 40),
                Size = new Size(600, 400),
                View = View.LargeIcon,
                LargeImageList = thumbnailImageList,
                HideSelection = false,
                MultiSelect = false,
                FullRowSelect = true
            };
            windowListView.SelectedIndexChanged += WindowListView_SelectedIndexChanged;
            windowListView.DoubleClick += WindowListView_DoubleClick;

            // ボタン
            selectButton = new Button
            {
                Text = "選択",
                Location = new Point(456, 450),
                Size = new Size(75, 23),
                Enabled = false
            };
            selectButton.Click += SelectButton_Click;

            cancelButton = new Button
            {
                Text = "キャンセル",
                Location = new Point(537, 450),
                Size = new Size(75, 23)
            };
            cancelButton.Click += CancelButton_Click;

            // コントロールの追加
            this.Controls.Add(instructionLabel);
            this.Controls.Add(windowListView);
            this.Controls.Add(selectButton);
            this.Controls.Add(cancelButton);

            // キャンセル動作の設定
            this.CancelButton = cancelButton;
        }

        /// <summary>
        /// ウィンドウリストの読み込み
        /// </summary>
        private void LoadWindowList()
        {
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                windowListView.Items.Clear();
                thumbnailImageList.Images.Clear();

                // ウィンドウリストの取得
                var windows = WindowSelector.GetAllWindows();

                int imageIndex = 0;
                foreach (var window in windows)
                {
                    if (window.Thumbnail != null)
                    {
                        thumbnailImageList.Images.Add(window.Thumbnail);

                        ListViewItem item = new ListViewItem
                        {
                            Text = window.Title,
                            ImageIndex = imageIndex++,
                            Tag = window
                        };

                        windowListView.Items.Add(item);
                    }
                }

                Debug.WriteLine($"{windowListView.Items.Count}個のウィンドウをリストに読み込みました");

                // ウィンドウが見つからない場合
                if (windowListView.Items.Count == 0)
                {
                    MessageBox.Show(
                        "表示可能なウィンドウが見つかりませんでした。\n別のアプリケーションを起動してからもう一度お試しください。",
                        "情報",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ウィンドウリストの読み込み中にエラーが発生しました: {ex.Message}");
                MessageBox.Show(
                    $"ウィンドウの列挙中にエラーが発生しました: {ex.Message}",
                    "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        /// <summary>
        /// ウィンドウリストの選択変更イベント
        /// </summary>
        private void WindowListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectButton.Enabled = windowListView.SelectedItems.Count > 0;
        }

        /// <summary>
        /// ウィンドウリストのダブルクリックイベント
        /// </summary>
        private void WindowListView_DoubleClick(object sender, EventArgs e)
        {
            if (windowListView.SelectedItems.Count > 0)
            {
                SelectCurrentWindow();
            }
        }

        /// <summary>
        /// 選択ボタンのクリックイベント
        /// </summary>
        private void SelectButton_Click(object sender, EventArgs e)
        {
            SelectCurrentWindow();
        }

        /// <summary>
        /// キャンセルボタンのクリックイベント
        /// </summary>
        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// 現在選択されているウィンドウを選択
        /// </summary>
        private void SelectCurrentWindow()
        {
            if (windowListView.SelectedItems.Count > 0)
            {
                SelectedWindow = windowListView.SelectedItems[0].Tag as WindowInfo;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        /// <summary>
        /// リソースの解放
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // サムネイル画像の解放
                if (thumbnailImageList != null)
                {
                    foreach (Image img in thumbnailImageList.Images)
                    {
                        img.Dispose();
                    }
                    thumbnailImageList.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}