namespace RIP组件
{
    partial class 数据处理进度
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(数据处理进度));
            this.SaveRIPFileBWorks = new System.ComponentModel.BackgroundWorker();
            this.XLabel = new System.Windows.Forms.TextBox();
            this.NetDataBtn = new System.Windows.Forms.Button();
            this.ParseBtn = new System.Windows.Forms.Button();
            this.IPCStartBtn = new System.Windows.Forms.Button();
            this.InformationBox = new System.Windows.Forms.TextBox();
            this.JobsProgressBar = new System.Windows.Forms.ProgressBar();
            this.SuspendLayout();
            // 
            // SaveRIPFileBWorks
            // 
            this.SaveRIPFileBWorks.DoWork += new System.ComponentModel.DoWorkEventHandler(this.SaveRIPFileBWorks_DoWork);
            this.SaveRIPFileBWorks.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.SaveRIPFileBWorks_ProgressChanged);
            this.SaveRIPFileBWorks.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.SaveRIPFileBWorks_RunWorkerCompleted);
            // 
            // XLabel
            // 
            this.XLabel.BackColor = System.Drawing.Color.GreenYellow;
            this.XLabel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.XLabel.Location = new System.Drawing.Point(363, 77);
            this.XLabel.Name = "XLabel";
            this.XLabel.Size = new System.Drawing.Size(86, 21);
            this.XLabel.TabIndex = 31;
            this.XLabel.Text = "0";
            this.XLabel.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // NetDataBtn
            // 
            this.NetDataBtn.BackColor = System.Drawing.Color.MintCream;
            this.NetDataBtn.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("NetDataBtn.BackgroundImage")));
            this.NetDataBtn.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.NetDataBtn.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.NetDataBtn.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.NetDataBtn.Location = new System.Drawing.Point(363, 104);
            this.NetDataBtn.Name = "NetDataBtn";
            this.NetDataBtn.Size = new System.Drawing.Size(86, 49);
            this.NetDataBtn.TabIndex = 30;
            this.NetDataBtn.Text = "开启\r\n远程";
            this.NetDataBtn.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.NetDataBtn.UseVisualStyleBackColor = true;
            this.NetDataBtn.Click += new System.EventHandler(this.NetDataBtn_Click);
            // 
            // ParseBtn
            // 
            this.ParseBtn.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.ParseBtn.Location = new System.Drawing.Point(363, 167);
            this.ParseBtn.Name = "ParseBtn";
            this.ParseBtn.Size = new System.Drawing.Size(86, 49);
            this.ParseBtn.TabIndex = 29;
            this.ParseBtn.Text = "解析本地远程对象";
            this.ParseBtn.UseVisualStyleBackColor = true;
            this.ParseBtn.Click += new System.EventHandler(this.ParseBtn_Click);
            // 
            // IPCStartBtn
            // 
            this.IPCStartBtn.Location = new System.Drawing.Point(363, 43);
            this.IPCStartBtn.Name = "IPCStartBtn";
            this.IPCStartBtn.Size = new System.Drawing.Size(86, 26);
            this.IPCStartBtn.TabIndex = 27;
            this.IPCStartBtn.Text = "IPC开始通讯";
            this.IPCStartBtn.UseVisualStyleBackColor = true;
            this.IPCStartBtn.Click += new System.EventHandler(this.IPCStartBtn_Click);
            // 
            // InformationBox
            // 
            this.InformationBox.BackColor = System.Drawing.Color.Khaki;
            this.InformationBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.InformationBox.Location = new System.Drawing.Point(12, 43);
            this.InformationBox.Multiline = true;
            this.InformationBox.Name = "InformationBox";
            this.InformationBox.Size = new System.Drawing.Size(345, 182);
            this.InformationBox.TabIndex = 26;
            // 
            // JobsProgressBar
            // 
            this.JobsProgressBar.ForeColor = System.Drawing.Color.LawnGreen;
            this.JobsProgressBar.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.JobsProgressBar.Location = new System.Drawing.Point(12, 6);
            this.JobsProgressBar.MarqueeAnimationSpeed = 300;
            this.JobsProgressBar.Name = "JobsProgressBar";
            this.JobsProgressBar.Size = new System.Drawing.Size(385, 27);
            this.JobsProgressBar.Step = 1;
            this.JobsProgressBar.TabIndex = 32;
            // 
            // 数据处理进度
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(412, 39);
            this.Controls.Add(this.JobsProgressBar);
            this.Controls.Add(this.XLabel);
            this.Controls.Add(this.NetDataBtn);
            this.Controls.Add(this.ParseBtn);
            this.Controls.Add(this.IPCStartBtn);
            this.Controls.Add(this.InformationBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "数据处理进度";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "RIP进度";
            this.WindowState = System.Windows.Forms.FormWindowState.Minimized;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.ComponentModel.BackgroundWorker SaveRIPFileBWorks;
        private System.Windows.Forms.TextBox XLabel;
        private System.Windows.Forms.Button NetDataBtn;
        private System.Windows.Forms.Button ParseBtn;
        private System.Windows.Forms.Button IPCStartBtn;
        private System.Windows.Forms.TextBox InformationBox;
        private System.Windows.Forms.ProgressBar JobsProgressBar;
    }
}

