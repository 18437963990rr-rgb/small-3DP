namespace BinderJetting
{
    partial class SendMessageToCamera
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            if (mmf != null)//20230113新建且批注：避免潜在的隐患
            {
                mmf.Dispose();
            }

            if (mutex != null)//20230113新建且批注：避免潜在的隐患
            {
                mutex.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SendMessageToCamera));
            this.label5 = new System.Windows.Forms.Label();
            this.button4 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.button2 = new System.Windows.Forms.Button();
            this.textBox4 = new System.Windows.Forms.TextBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.打印监控项目 = new System.Windows.Forms.GroupBox();
            this.checkBox12 = new System.Windows.Forms.CheckBox();
            this.checkBox11 = new System.Windows.Forms.CheckBox();
            this.checkBox10 = new System.Windows.Forms.CheckBox();
            this.checkBox9 = new System.Windows.Forms.CheckBox();
            this.checkBox8 = new System.Windows.Forms.CheckBox();
            this.label9 = new System.Windows.Forms.Label();
            this.checkBox7 = new System.Windows.Forms.CheckBox();
            this.checkBox6 = new System.Windows.Forms.CheckBox();
            this.checkBox5 = new System.Windows.Forms.CheckBox();
            this.checkBox4 = new System.Windows.Forms.CheckBox();
            this.checkBox3 = new System.Windows.Forms.CheckBox();
            this.checkBox2 = new System.Windows.Forms.CheckBox();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.label8 = new System.Windows.Forms.Label();
            this.打印监控项目.SuspendLayout();
            this.SuspendLayout();
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.8F);
            this.label5.Location = new System.Drawing.Point(9, 151);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(128, 18);
            this.label5.TabIndex = 25;
            this.label5.Text = "监控记录主路径：";
            // 
            // button4
            // 
            this.button4.BackColor = System.Drawing.Color.Transparent;
            this.button4.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.button4.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button4.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.button4.Image = ((System.Drawing.Image)(resources.GetObject("button4.Image")));
            this.button4.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.button4.Location = new System.Drawing.Point(626, 155);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(123, 51);
            this.button4.TabIndex = 24;
            this.button4.Text = "保存配置";
            this.button4.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.button4.UseVisualStyleBackColor = false;
            // 
            // button3
            // 
            this.button3.BackColor = System.Drawing.Color.Transparent;
            this.button3.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.button3.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button3.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.button3.Image = ((System.Drawing.Image)(resources.GetObject("button3.Image")));
            this.button3.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.button3.Location = new System.Drawing.Point(626, 213);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(123, 51);
            this.button3.TabIndex = 23;
            this.button3.Text = "退出模块";
            this.button3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.button3.UseVisualStyleBackColor = false;
            // 
            // textBox3
            // 
            this.textBox3.Font = new System.Drawing.Font("SimSun", 10F);
            this.textBox3.Location = new System.Drawing.Point(171, 204);
            this.textBox3.Name = "textBox3";
            this.textBox3.Size = new System.Drawing.Size(391, 23);
            this.textBox3.TabIndex = 22;
            this.textBox3.Text = "1";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.8F);
            this.label4.Location = new System.Drawing.Point(9, 205);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(158, 18);
            this.label4.TabIndex = 21;
            this.label4.Text = "打印层数(记录-测试)：";
            // 
            // label3
            // 
            this.label3.BackColor = System.Drawing.Color.LightPink;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.label3.ForeColor = System.Drawing.Color.Blue;
            this.label3.Location = new System.Drawing.Point(15, 280);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(760, 106);
            this.label3.TabIndex = 20;
            this.label3.Text = "格式规范及输入提示：\r\n请勿在打印任务名称框输入特殊字符，如： / : * ？ \\\" < > | \r\n记录路径格式：主路径\\[Record][230107][打印" +
    "任务名称]\r\n记录文件格式：[230107-081931-PM][第1层-工艺时刻][Light115-Expo100-Gain30].bmp";
            // 
            // textBox1
            // 
            this.textBox1.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.textBox1.Location = new System.Drawing.Point(171, 148);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.Size = new System.Drawing.Size(391, 23);
            this.textBox1.TabIndex = 19;
            // 
            // button2
            // 
            this.button2.BackColor = System.Drawing.Color.Transparent;
            this.button2.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.button2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.button2.ForeColor = System.Drawing.Color.Black;
            this.button2.Image = ((System.Drawing.Image)(resources.GetObject("button2.Image")));
            this.button2.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.button2.Location = new System.Drawing.Point(609, 28);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(157, 49);
            this.button2.TabIndex = 18;
            this.button2.Text = "更新记录路径";
            this.button2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.button2.UseVisualStyleBackColor = false;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // textBox4
            // 
            this.textBox4.Font = new System.Drawing.Font("SimSun", 10F);
            this.textBox4.Location = new System.Drawing.Point(171, 231);
            this.textBox4.Name = "textBox4";
            this.textBox4.Size = new System.Drawing.Size(391, 23);
            this.textBox4.TabIndex = 15;
            this.textBox4.Text = "铺粉后";
            // 
            // textBox2
            // 
            this.textBox2.Font = new System.Drawing.Font("SimSun", 10F);
            this.textBox2.Location = new System.Drawing.Point(171, 175);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(391, 23);
            this.textBox2.TabIndex = 14;
            // 
            // button1
            // 
            this.button1.BackColor = System.Drawing.Color.Transparent;
            this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.button1.ForeColor = System.Drawing.Color.Black;
            this.button1.Image = ((System.Drawing.Image)(resources.GetObject("button1.Image")));
            this.button1.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.button1.Location = new System.Drawing.Point(609, 83);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(157, 49);
            this.button1.TabIndex = 13;
            this.button1.Text = "拍摄记录测试";
            this.button1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.button1.UseVisualStyleBackColor = false;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.8F);
            this.label2.Location = new System.Drawing.Point(9, 231);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(158, 18);
            this.label2.TabIndex = 17;
            this.label2.Text = "工艺时刻(记录-测试)：";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.8F);
            this.label1.Location = new System.Drawing.Point(9, 178);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(113, 18);
            this.label1.TabIndex = 16;
            this.label1.Text = "打印任务名称：";
            // 
            // label6
            // 
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.8F);
            this.label6.Location = new System.Drawing.Point(20, 22);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(57, 118);
            this.label6.TabIndex = 30;
            this.label6.Text = "喷墨过程监控记录点";
            // 
            // label7
            // 
            this.label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.8F);
            this.label7.Location = new System.Drawing.Point(294, 22);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(54, 87);
            this.label7.TabIndex = 31;
            this.label7.Text = "铺粉过程监控记录点";
            // 
            // 打印监控项目
            // 
            this.打印监控项目.Controls.Add(this.textBox3);
            this.打印监控项目.Controls.Add(this.textBox4);
            this.打印监控项目.Controls.Add(this.textBox2);
            this.打印监控项目.Controls.Add(this.textBox1);
            this.打印监控项目.Controls.Add(this.checkBox12);
            this.打印监控项目.Controls.Add(this.checkBox11);
            this.打印监控项目.Controls.Add(this.checkBox10);
            this.打印监控项目.Controls.Add(this.checkBox9);
            this.打印监控项目.Controls.Add(this.checkBox8);
            this.打印监控项目.Controls.Add(this.label9);
            this.打印监控项目.Controls.Add(this.checkBox7);
            this.打印监控项目.Controls.Add(this.checkBox6);
            this.打印监控项目.Controls.Add(this.checkBox5);
            this.打印监控项目.Controls.Add(this.checkBox4);
            this.打印监控项目.Controls.Add(this.checkBox3);
            this.打印监控项目.Controls.Add(this.checkBox2);
            this.打印监控项目.Controls.Add(this.checkBox1);
            this.打印监控项目.Controls.Add(this.label8);
            this.打印监控项目.Controls.Add(this.label6);
            this.打印监控项目.Controls.Add(this.label5);
            this.打印监控项目.Controls.Add(this.label7);
            this.打印监控项目.Controls.Add(this.label4);
            this.打印监控项目.Controls.Add(this.label2);
            this.打印监控项目.Controls.Add(this.label1);
            this.打印监控项目.Location = new System.Drawing.Point(15, 9);
            this.打印监控项目.Name = "打印监控项目";
            this.打印监控项目.Size = new System.Drawing.Size(571, 265);
            this.打印监控项目.TabIndex = 32;
            this.打印监控项目.TabStop = false;
            this.打印监控项目.Text = "打印监控项目";
            // 
            // checkBox12
            // 
            this.checkBox12.BackColor = System.Drawing.SystemColors.Window;
            this.checkBox12.Location = new System.Drawing.Point(358, 87);
            this.checkBox12.Name = "checkBox12";
            this.checkBox12.Size = new System.Drawing.Size(200, 16);
            this.checkBox12.TabIndex = 45;
            this.checkBox12.Text = "BackPowderStation";
            this.checkBox12.UseVisualStyleBackColor = false;
            // 
            // checkBox11
            // 
            this.checkBox11.BackColor = System.Drawing.SystemColors.Window;
            this.checkBox11.Enabled = false;
            this.checkBox11.Location = new System.Drawing.Point(358, 70);
            this.checkBox11.Name = "checkBox11";
            this.checkBox11.Size = new System.Drawing.Size(200, 16);
            this.checkBox11.TabIndex = 44;
            this.checkBox11.Text = "Recoat-InRecedeCut";
            this.checkBox11.UseVisualStyleBackColor = false;
            // 
            // checkBox10
            // 
            this.checkBox10.BackColor = System.Drawing.SystemColors.Window;
            this.checkBox10.Location = new System.Drawing.Point(358, 53);
            this.checkBox10.Name = "checkBox10";
            this.checkBox10.Size = new System.Drawing.Size(200, 16);
            this.checkBox10.TabIndex = 43;
            this.checkBox10.Text = "Recoat-RightArrived";
            this.checkBox10.UseVisualStyleBackColor = false;
            // 
            // checkBox9
            // 
            this.checkBox9.BackColor = System.Drawing.SystemColors.Window;
            this.checkBox9.Enabled = false;
            this.checkBox9.Location = new System.Drawing.Point(358, 36);
            this.checkBox9.Name = "checkBox9";
            this.checkBox9.Size = new System.Drawing.Size(200, 16);
            this.checkBox9.TabIndex = 42;
            this.checkBox9.Text = "Recoat-InAdvanceCut";
            this.checkBox9.UseVisualStyleBackColor = false;
            // 
            // checkBox8
            // 
            this.checkBox8.BackColor = System.Drawing.SystemColors.Window;
            this.checkBox8.Location = new System.Drawing.Point(358, 19);
            this.checkBox8.Name = "checkBox8";
            this.checkBox8.Size = new System.Drawing.Size(200, 16);
            this.checkBox8.TabIndex = 41;
            this.checkBox8.Text = "LeavePowderStation";
            this.checkBox8.UseVisualStyleBackColor = false;
            // 
            // label9
            // 
            this.label9.BackColor = System.Drawing.SystemColors.Window;
            this.label9.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.label9.Location = new System.Drawing.Point(354, 15);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(205, 90);
            this.label9.TabIndex = 46;
            // 
            // checkBox7
            // 
            this.checkBox7.BackColor = System.Drawing.SystemColors.Window;
            this.checkBox7.Location = new System.Drawing.Point(80, 121);
            this.checkBox7.Name = "checkBox7";
            this.checkBox7.Size = new System.Drawing.Size(200, 16);
            this.checkBox7.TabIndex = 38;
            this.checkBox7.Text = "BackCleanStation(After-6PASS)";
            this.checkBox7.UseVisualStyleBackColor = false;
            // 
            // checkBox6
            // 
            this.checkBox6.BackColor = System.Drawing.SystemColors.Window;
            this.checkBox6.Location = new System.Drawing.Point(80, 104);
            this.checkBox6.Name = "checkBox6";
            this.checkBox6.Size = new System.Drawing.Size(200, 16);
            this.checkBox6.TabIndex = 37;
            this.checkBox6.Text = "5PASS-AfterCut";
            this.checkBox6.UseVisualStyleBackColor = false;
            // 
            // checkBox5
            // 
            this.checkBox5.BackColor = System.Drawing.SystemColors.Window;
            this.checkBox5.Location = new System.Drawing.Point(80, 87);
            this.checkBox5.Name = "checkBox5";
            this.checkBox5.Size = new System.Drawing.Size(200, 16);
            this.checkBox5.TabIndex = 36;
            this.checkBox5.Text = "4PASS-AfterCut";
            this.checkBox5.UseVisualStyleBackColor = false;
            // 
            // checkBox4
            // 
            this.checkBox4.BackColor = System.Drawing.SystemColors.Window;
            this.checkBox4.Location = new System.Drawing.Point(80, 70);
            this.checkBox4.Name = "checkBox4";
            this.checkBox4.Size = new System.Drawing.Size(200, 16);
            this.checkBox4.TabIndex = 35;
            this.checkBox4.Text = "3PASS-AfterCut";
            this.checkBox4.UseVisualStyleBackColor = false;
            // 
            // checkBox3
            // 
            this.checkBox3.BackColor = System.Drawing.SystemColors.Window;
            this.checkBox3.Location = new System.Drawing.Point(80, 53);
            this.checkBox3.Name = "checkBox3";
            this.checkBox3.Size = new System.Drawing.Size(200, 16);
            this.checkBox3.TabIndex = 34;
            this.checkBox3.Text = "2PASS-AfterCut";
            this.checkBox3.UseVisualStyleBackColor = false;
            // 
            // checkBox2
            // 
            this.checkBox2.BackColor = System.Drawing.SystemColors.Window;
            this.checkBox2.Location = new System.Drawing.Point(80, 36);
            this.checkBox2.Name = "checkBox2";
            this.checkBox2.Size = new System.Drawing.Size(200, 16);
            this.checkBox2.TabIndex = 33;
            this.checkBox2.Text = "1PASS-AfterCut";
            this.checkBox2.UseVisualStyleBackColor = false;
            // 
            // checkBox1
            // 
            this.checkBox1.BackColor = System.Drawing.SystemColors.Window;
            this.checkBox1.Location = new System.Drawing.Point(80, 19);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(200, 16);
            this.checkBox1.TabIndex = 32;
            this.checkBox1.Text = "LeaveCleanStation";
            this.checkBox1.UseVisualStyleBackColor = false;
            // 
            // label8
            // 
            this.label8.BackColor = System.Drawing.SystemColors.Window;
            this.label8.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.label8.Location = new System.Drawing.Point(76, 15);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(205, 125);
            this.label8.TabIndex = 40;
            // 
            // SendMessageToCamera
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(787, 395);
            this.Controls.Add(this.打印监控项目);
            this.Controls.Add(this.button4);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SendMessageToCamera";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "SendMessageToCamera";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.打印监控项目.ResumeLayout(false);
            this.打印监控项目.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.TextBox textBox3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.TextBox textBox4;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.GroupBox 打印监控项目;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.CheckBox checkBox7;
        private System.Windows.Forms.CheckBox checkBox6;
        private System.Windows.Forms.CheckBox checkBox5;
        private System.Windows.Forms.CheckBox checkBox4;
        private System.Windows.Forms.CheckBox checkBox3;
        private System.Windows.Forms.CheckBox checkBox2;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.CheckBox checkBox12;
        private System.Windows.Forms.CheckBox checkBox11;
        private System.Windows.Forms.CheckBox checkBox10;
        private System.Windows.Forms.CheckBox checkBox9;
        private System.Windows.Forms.CheckBox checkBox8;
        private System.Windows.Forms.Label label9;
    }
}