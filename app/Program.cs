using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using GameTranslationOverlay.Core.Configuration;
using GameTranslationOverlay.Core.Licensing;

namespace GameTranslationOverlay
{
    static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                // アプリケーション設定の初期化
                Debug.WriteLine("アプリケーション設定を初期化しています...");
                var settings = AppSettings.Instance;
                Debug.WriteLine($"設定を読み込みました: バージョン={settings.AppVersion}, デバッグモード={settings.DebugModeEnabled}");

                // ライセンス管理の初期化
                Debug.WriteLine("ライセンス状態を初期化しています...");
                var licenseManager = LicenseManager.Instance;
                Debug.WriteLine($"ライセンス状態: タイプ={licenseManager.CurrentLicenseType}, 有効={licenseManager.IsLicenseValid}");

                // アプリケーション表示設定
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // メインフォームを起動
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"アプリケーション起動中にエラーが発生しました: {ex.Message}");
                MessageBox.Show(
                    $"アプリケーションの初期化中にエラーが発生しました。\n\n{ex.Message}",
                    "起動エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}