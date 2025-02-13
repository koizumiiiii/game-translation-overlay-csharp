namespace GameTranslationOverlay
{
    partial class MainForm
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージド リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナーで生成されたコード

        /// <summary>
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            this.btnEnable = new System.Windows.Forms.Button();
            this.grpDetectionMode = new System.Windows.Forms.GroupBox();
            this.rbFullScreen = new System.Windows.Forms.RadioButton();
            this.rbSelectedRegion = new System.Windows.Forms.RadioButton();
            this.btnSelectRegion = new System.Windows.Forms.Button();
            this.grpDetectionMode.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnEnable
            // 
            this.btnEnable.Location = new System.Drawing.Point(12, 12);
            this.btnEnable.Name = "btnEnable";
            this.btnEnable.Size = new System.Drawing.Size(75, 23);
            this.btnEnable.TabIndex = 0;
            this.btnEnable.Text = "Enable Overlay";
            this.btnEnable.UseVisualStyleBackColor = true;
            // 
            // grpDetectionMode
            // 
            this.grpDetectionMode.Controls.Add(this.rbSelectedRegion);
            this.grpDetectionMode.Controls.Add(this.rbFullScreen);
            this.grpDetectionMode.Location = new System.Drawing.Point(12, 49);
            this.grpDetectionMode.Name = "grpDetectionMode";
            this.grpDetectionMode.Size = new System.Drawing.Size(200, 61);
            this.grpDetectionMode.TabIndex = 1;
            this.grpDetectionMode.TabStop = false;
            this.grpDetectionMode.Text = "Detection Mode";
            // 
            // rbFullScreen
            // 
            this.rbFullScreen.AutoSize = true;
            this.rbFullScreen.Checked = true;
            this.rbFullScreen.Location = new System.Drawing.Point(7, 19);
            this.rbFullScreen.Name = "rbFullScreen";
            this.rbFullScreen.Size = new System.Drawing.Size(81, 16);
            this.rbFullScreen.TabIndex = 0;
            this.rbFullScreen.TabStop = true;
            this.rbFullScreen.Text = "Full Screen";
            this.rbFullScreen.UseVisualStyleBackColor = true;
            // 
            // rbSelectedRegion
            // 
            this.rbSelectedRegion.AutoSize = true;
            this.rbSelectedRegion.Location = new System.Drawing.Point(7, 42);
            this.rbSelectedRegion.Name = "rbSelectedRegion";
            this.rbSelectedRegion.Size = new System.Drawing.Size(106, 16);
            this.rbSelectedRegion.TabIndex = 1;
            this.rbSelectedRegion.Text = "Selected Region";
            this.rbSelectedRegion.UseVisualStyleBackColor = true;
            // 
            // btnSelectRegion
            // 
            this.btnSelectRegion.Location = new System.Drawing.Point(12, 126);
            this.btnSelectRegion.Name = "btnSelectRegion";
            this.btnSelectRegion.Size = new System.Drawing.Size(75, 23);
            this.btnSelectRegion.TabIndex = 2;
            this.btnSelectRegion.Text = "Select Region";
            this.btnSelectRegion.UseVisualStyleBackColor = true;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 161);
            this.Controls.Add(this.btnSelectRegion);
            this.Controls.Add(this.grpDetectionMode);
            this.Controls.Add(this.btnEnable);
            this.Name = "MainForm";
            this.Text = "Game Translation";
            this.grpDetectionMode.ResumeLayout(false);
            this.grpDetectionMode.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnEnable;
        private System.Windows.Forms.GroupBox grpDetectionMode;
        private System.Windows.Forms.RadioButton rbSelectedRegion;
        private System.Windows.Forms.RadioButton rbFullScreen;
        private System.Windows.Forms.Button btnSelectRegion;
    }
}

