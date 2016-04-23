namespace HiddenWallet.UserInterface
{
    partial class FormSettings
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
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormSettings));
            this.flowLayoutPanelSettings = new System.Windows.Forms.FlowLayoutPanel();
            this.panelWalletFile = new System.Windows.Forms.Panel();
            this.buttonWalletFileBrowse = new System.Windows.Forms.Button();
            this.textBoxWalletFilePath = new System.Windows.Forms.TextBox();
            this.labelWalletFile = new System.Windows.Forms.Label();
            this.panelNetwork = new System.Windows.Forms.Panel();
            this.comboBoxNetwork = new System.Windows.Forms.ComboBox();
            this.labelNetwork = new System.Windows.Forms.Label();
            this.panelAddressCount = new System.Windows.Forms.Panel();
            this.textBoxAddressCount = new System.Windows.Forms.TextBox();
            this.labelAddressCount = new System.Windows.Forms.Label();
            this.panelClose = new System.Windows.Forms.Panel();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonApply = new System.Windows.Forms.Button();
            this.openFileDialogWalletFile = new System.Windows.Forms.OpenFileDialog();
            this.flowLayoutPanelSettings.SuspendLayout();
            this.panelWalletFile.SuspendLayout();
            this.panelNetwork.SuspendLayout();
            this.panelAddressCount.SuspendLayout();
            this.panelClose.SuspendLayout();
            this.SuspendLayout();
            // 
            // flowLayoutPanelSettings
            // 
            this.flowLayoutPanelSettings.Controls.Add(this.panelWalletFile);
            this.flowLayoutPanelSettings.Controls.Add(this.panelNetwork);
            this.flowLayoutPanelSettings.Controls.Add(this.panelAddressCount);
            this.flowLayoutPanelSettings.Controls.Add(this.panelClose);
            this.flowLayoutPanelSettings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanelSettings.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanelSettings.Name = "flowLayoutPanelSettings";
            this.flowLayoutPanelSettings.Size = new System.Drawing.Size(413, 143);
            this.flowLayoutPanelSettings.TabIndex = 0;
            // 
            // panelWalletFile
            // 
            this.panelWalletFile.Controls.Add(this.buttonWalletFileBrowse);
            this.panelWalletFile.Controls.Add(this.textBoxWalletFilePath);
            this.panelWalletFile.Controls.Add(this.labelWalletFile);
            this.panelWalletFile.Location = new System.Drawing.Point(3, 3);
            this.panelWalletFile.Name = "panelWalletFile";
            this.panelWalletFile.Size = new System.Drawing.Size(410, 30);
            this.panelWalletFile.TabIndex = 3;
            // 
            // buttonWalletFileBrowse
            // 
            this.buttonWalletFileBrowse.Location = new System.Drawing.Point(323, 4);
            this.buttonWalletFileBrowse.Name = "buttonWalletFileBrowse";
            this.buttonWalletFileBrowse.Size = new System.Drawing.Size(75, 23);
            this.buttonWalletFileBrowse.TabIndex = 2;
            this.buttonWalletFileBrowse.Text = "Browse";
            this.buttonWalletFileBrowse.UseVisualStyleBackColor = true;
            this.buttonWalletFileBrowse.Click += new System.EventHandler(this.buttonWalletFileBrowse_Click);
            // 
            // textBoxWalletFilePath
            // 
            this.textBoxWalletFilePath.Location = new System.Drawing.Point(71, 6);
            this.textBoxWalletFilePath.Name = "textBoxWalletFilePath";
            this.textBoxWalletFilePath.ReadOnly = true;
            this.textBoxWalletFilePath.Size = new System.Drawing.Size(246, 20);
            this.textBoxWalletFilePath.TabIndex = 1;
            this.textBoxWalletFilePath.TextChanged += new System.EventHandler(this.textBoxWalletFilePath_TextChanged);
            // 
            // labelWalletFile
            // 
            this.labelWalletFile.AutoSize = true;
            this.labelWalletFile.Location = new System.Drawing.Point(9, 9);
            this.labelWalletFile.Name = "labelWalletFile";
            this.labelWalletFile.Size = new System.Drawing.Size(56, 13);
            this.labelWalletFile.TabIndex = 0;
            this.labelWalletFile.Text = "Wallet file:";
            // 
            // panelNetwork
            // 
            this.panelNetwork.Controls.Add(this.comboBoxNetwork);
            this.panelNetwork.Controls.Add(this.labelNetwork);
            this.panelNetwork.Location = new System.Drawing.Point(3, 39);
            this.panelNetwork.Name = "panelNetwork";
            this.panelNetwork.Size = new System.Drawing.Size(410, 30);
            this.panelNetwork.TabIndex = 2;
            // 
            // comboBoxNetwork
            // 
            this.comboBoxNetwork.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxNetwork.FormattingEnabled = true;
            this.comboBoxNetwork.Location = new System.Drawing.Point(71, 6);
            this.comboBoxNetwork.Name = "comboBoxNetwork";
            this.comboBoxNetwork.Size = new System.Drawing.Size(135, 21);
            this.comboBoxNetwork.TabIndex = 1;
            this.comboBoxNetwork.SelectedIndexChanged += new System.EventHandler(this.comboBoxNetwork_SelectedIndexChanged);
            // 
            // labelNetwork
            // 
            this.labelNetwork.AutoSize = true;
            this.labelNetwork.Location = new System.Drawing.Point(9, 9);
            this.labelNetwork.Name = "labelNetwork";
            this.labelNetwork.Size = new System.Drawing.Size(50, 13);
            this.labelNetwork.TabIndex = 0;
            this.labelNetwork.Text = "Network:";
            // 
            // panelAddressCount
            // 
            this.panelAddressCount.Controls.Add(this.textBoxAddressCount);
            this.panelAddressCount.Controls.Add(this.labelAddressCount);
            this.panelAddressCount.Location = new System.Drawing.Point(3, 75);
            this.panelAddressCount.Name = "panelAddressCount";
            this.panelAddressCount.Size = new System.Drawing.Size(410, 30);
            this.panelAddressCount.TabIndex = 4;
            // 
            // textBoxAddressCount
            // 
            this.textBoxAddressCount.Location = new System.Drawing.Point(71, 6);
            this.textBoxAddressCount.Name = "textBoxAddressCount";
            this.textBoxAddressCount.Size = new System.Drawing.Size(30, 20);
            this.textBoxAddressCount.TabIndex = 3;
            this.textBoxAddressCount.TextChanged += new System.EventHandler(this.textBoxAddressCount_TextChanged);
            // 
            // labelAddressCount
            // 
            this.labelAddressCount.AutoSize = true;
            this.labelAddressCount.Location = new System.Drawing.Point(9, 9);
            this.labelAddressCount.Name = "labelAddressCount";
            this.labelAddressCount.Size = new System.Drawing.Size(39, 13);
            this.labelAddressCount.TabIndex = 0;
            this.labelAddressCount.Text = "Depth:";
            // 
            // panelClose
            // 
            this.panelClose.Controls.Add(this.buttonCancel);
            this.panelClose.Controls.Add(this.buttonApply);
            this.panelClose.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelClose.Location = new System.Drawing.Point(3, 111);
            this.panelClose.Name = "panelClose";
            this.panelClose.Size = new System.Drawing.Size(410, 30);
            this.panelClose.TabIndex = 3;
            // 
            // buttonCancel
            // 
            this.buttonCancel.Location = new System.Drawing.Point(242, 4);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 3;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // buttonApply
            // 
            this.buttonApply.Location = new System.Drawing.Point(323, 3);
            this.buttonApply.Name = "buttonApply";
            this.buttonApply.Size = new System.Drawing.Size(75, 23);
            this.buttonApply.TabIndex = 4;
            this.buttonApply.Text = "Apply";
            this.buttonApply.UseVisualStyleBackColor = true;
            this.buttonApply.Click += new System.EventHandler(this.buttonApply_Click);
            // 
            // FormSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(413, 143);
            this.Controls.Add(this.flowLayoutPanelSettings);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.Name = "FormSettings";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "HiddenWallet: Settings";
            this.Load += new System.EventHandler(this.FormSettings_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FormSettings_KeyDown);
            this.flowLayoutPanelSettings.ResumeLayout(false);
            this.panelWalletFile.ResumeLayout(false);
            this.panelWalletFile.PerformLayout();
            this.panelNetwork.ResumeLayout(false);
            this.panelNetwork.PerformLayout();
            this.panelAddressCount.ResumeLayout(false);
            this.panelAddressCount.PerformLayout();
            this.panelClose.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelSettings;
        private System.Windows.Forms.Panel panelNetwork;
        private System.Windows.Forms.ComboBox comboBoxNetwork;
        private System.Windows.Forms.Label labelNetwork;
        private System.Windows.Forms.Panel panelClose;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonApply;
        private System.Windows.Forms.Panel panelWalletFile;
        private System.Windows.Forms.Button buttonWalletFileBrowse;
        private System.Windows.Forms.TextBox textBoxWalletFilePath;
        private System.Windows.Forms.Label labelWalletFile;
        private System.Windows.Forms.OpenFileDialog openFileDialogWalletFile;
        private System.Windows.Forms.Panel panelAddressCount;
        private System.Windows.Forms.TextBox textBoxAddressCount;
        private System.Windows.Forms.Label labelAddressCount;
    }
}