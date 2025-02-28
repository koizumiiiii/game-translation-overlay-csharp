using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GameTranslationOverlay.Core.WindowManagement;

namespace GameTranslationOverlay.Forms
{
    public class WindowSelectorForm : Form
    {
        private ListView windowListView;
        private Button selectButton;
        private Button cancelButton;
        private Button refreshButton;
        private CheckBox showDesktopCheckBox;
        private TextBox searchBox;

        private ImageList thumbnailImageList;
        private List<WindowSelector.WindowInfo> allWindows;

        public WindowSelector.WindowInfo SelectedWindow { get; private set; }

        public WindowSelectorForm()
        {
            InitializeComponent();
            LoadWindowList();
        }

        private void InitializeComponent()
        {
            // フォーム設定
            this.Text = "翻訳対象ウィンドウの選択";
            this.Size = new Size(700, 500);
            this.MinimumSize = new Size(600, 400);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Icon = SystemIcons.Application;

            // 検索ボックス
            searchBox = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "ウィンドウ名で検索...",
                ForeColor = SystemColors.GrayText,
                Font = new Font("Yu Gothic UI", 9F)
            };

            // イベントハンドラを追加してプレースホルダーの動作を実装
            searchBox.Enter += (s, e) =>
            {
                if (searchBox.Text == "ウィンドウ名で検索...")
                {
                    searchBox.Text = "";
                    searchBox.ForeColor = SystemColors.WindowText;
                }
            };
            searchBox.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(searchBox.Text))
                {
                    searchBox.Text = "ウィンドウ名で検索...";
                    searchBox.ForeColor = SystemColors.GrayText;
                }
            };
            searchBox.TextChanged += SearchBox_TextChanged;

            // サムネイル用のイメージリスト
            thumbnailImageList = new ImageList
            {
                ImageSize = new Size(120, 80),
                ColorDepth = ColorDepth.Depth32Bit
            };

            // ウィンドウリストビュー
            windowListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Tile,
                LargeImageList = thumbnailImageList,
                TileSize = new Size(400, 100),
                Font = new Font("Yu Gothic UI", 9F),
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false
            };
            windowListView.SelectedIndexChanged += WindowListView_SelectedIndexChanged;
            windowListView.DoubleClick += WindowListView_DoubleClick;

            // デスクトップを表示するチェックボックス
            showDesktopCheckBox = new CheckBox
            {
                Text = "デスクトップを表示",
                Checked = false,
                AutoSize = true,
                Font = new Font("Yu Gothic UI", 9F)
            };
            showDesktopCheckBox.CheckedChanged += ShowDesktopCheckBox_CheckedChanged;

            // 更新ボタン
            refreshButton = new Button
            {
                Text = "更新",
                Width = 80,
                Height = 30,
                Font = new Font("Yu Gothic UI", 9F)
            };
            refreshButton.Click += RefreshButton_Click;

            // 選択ボタン
            selectButton = new Button
            {
                Text = "選択",
                Width = 80,
                Height = 30,
                Enabled = false,
                Font = new Font("Yu Gothic UI", 9F)
            };
            selectButton.Click += SelectButton_Click;

            // キャンセルボタン
            cancelButton = new Button
            {
                Text = "キャンセル",
                Width = 80,
                Height = 30,
                Font = new Font("Yu Gothic UI", 9F)
            };
            cancelButton.Click += CancelButton_Click;

            // ボタンパネル
            FlowLayoutPanel buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(5)
            };
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(selectButton);
            buttonPanel.Controls.Add(refreshButton);

            // 検索パネル
            Panel searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(5)
            };
            searchPanel.Controls.Add(searchBox);
            searchPanel.Controls.Add(showDesktopCheckBox);

            // チェックボックス位置調整
            showDesktopCheckBox.Location = new Point(
                searchBox.Right + 10,
                (searchPanel.Height - showDesktopCheckBox.Height) / 2
            );

            // コントロールの追加
            this.Controls.Add(windowListView);
            this.Controls.Add(buttonPanel);
            this.Controls.Add(searchPanel);

            // イベントハンドラの接続
            this.Load += WindowSelectorForm_Load;
            this.Resize += WindowSelectorForm_Resize;
        }

        private void WindowSelectorForm_Load(object sender, EventArgs e)
        {
            AdjustListViewColumns();
        }

        private void WindowSelectorForm_Resize(object sender, EventArgs e)
        {
            AdjustListViewColumns();
        }

        private void AdjustListViewColumns()
        {
            // タイルビューのサイズ調整
            windowListView.TileSize = new Size(windowListView.Width - 30, 100);
        }

        private void LoadWindowList()
        {
            // カーソルを待機カーソルに変更
            Cursor = Cursors.WaitCursor;

            try
            {
                windowListView.Items.Clear();
                thumbnailImageList.Images.Clear();

                // ウィンドウリストの取得
                allWindows = WindowSelector.GetWindows();

                // リストビューにウィンドウ情報を追加
                UpdateWindowList();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void UpdateWindowList()
        {
            windowListView.BeginUpdate();
            windowListView.Items.Clear();
            thumbnailImageList.Images.Clear();

            foreach (var window in allWindows)
            {
                // デスクトップ表示の制御
                if (!showDesktopCheckBox.Checked && (
                    window.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                    (window.Title.Equals("Program Manager", StringComparison.OrdinalIgnoreCase) ||
                     window.Title.Equals("プログラム マネージャー", StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                // 検索フィルタリング
                string searchText = searchBox.Text.Trim().ToLower();
                if (!string.IsNullOrEmpty(searchText) &&
                    !window.Title.ToLower().Contains(searchText) &&
                    !window.ProcessName.ToLower().Contains(searchText))
                {
                    continue;
                }

                // サムネイル画像の追加
                int imageIndex = -1;
                if (window.Thumbnail != null)
                {
                    thumbnailImageList.Images.Add(window.Thumbnail);
                    imageIndex = thumbnailImageList.Images.Count - 1;
                }

                // リストアイテムの作成
                ListViewItem item = new ListViewItem
                {
                    Text = window.Title,
                    ImageIndex = imageIndex,
                    Tag = window
                };

                // サブアイテムの追加
                item.SubItems.Add(window.ProcessName);
                item.SubItems.Add($"{window.Bounds.Width}x{window.Bounds.Height}");

                windowListView.Items.Add(item);
            }

            windowListView.EndUpdate();
        }

        private void WindowListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectButton.Enabled = windowListView.SelectedItems.Count > 0;
        }

        private void WindowListView_DoubleClick(object sender, EventArgs e)
        {
            if (windowListView.SelectedItems.Count > 0)
            {
                SelectCurrentWindow();
            }
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            LoadWindowList();
        }

        private void SelectButton_Click(object sender, EventArgs e)
        {
            SelectCurrentWindow();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void ShowDesktopCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateWindowList();
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            // プレースホルダーテキストの場合は検索しない
            if (searchBox.Text == "ウィンドウ名で検索...")
            {
                return;
            }

            UpdateWindowList();
        }

        private void SelectCurrentWindow()
        {
            if (windowListView.SelectedItems.Count > 0)
            {
                SelectedWindow = windowListView.SelectedItems[0].Tag as WindowSelector.WindowInfo;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
    }
}