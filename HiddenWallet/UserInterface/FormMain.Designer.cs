using System.Windows.Forms;
using HiddenWallet.UserInterface.Controls;

namespace HiddenWallet.UserInterface
{
    internal partial class FormMain
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.textBoxBalance = new System.Windows.Forms.TextBox();
            this.contextMenuStripBalance = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.syncWalletToolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStripMain = new System.Windows.Forms.MenuStrip();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.receiveAddressesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.actionsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.syncWalletToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.tabPageReceive = new System.Windows.Forms.TabPage();
            this.buttonGenerateNewAddress = new System.Windows.Forms.Button();
            this.textBoxRecieveAddress = new System.Windows.Forms.TextBox();
            this.contextMenuStripAddress = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewOnBlockchainToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewReceiveAddressesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tabPageSend = new System.Windows.Forms.TabPage();
            this.labelBtc = new System.Windows.Forms.Label();
            this.buttonAll = new System.Windows.Forms.Button();
            this.textBoxBtc = new HiddenWallet.UserInterface.Controls.CueTextBox();
            this.textBoxSendAddress = new HiddenWallet.UserInterface.Controls.CueTextBox();
            this.buttonSend = new System.Windows.Forms.Button();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            this.contextMenuStripBalance.SuspendLayout();
            this.menuStripMain.SuspendLayout();
            this.tabControlMain.SuspendLayout();
            this.tabPageReceive.SuspendLayout();
            this.contextMenuStripAddress.SuspendLayout();
            this.tabPageSend.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainerMain
            // 
            this.splitContainerMain.Location = new System.Drawing.Point(0, 0);
            this.splitContainerMain.Name = "splitContainerMain";
            this.splitContainerMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainerMain.Panel1
            // 
            this.splitContainerMain.Panel1.Controls.Add(this.textBoxBalance);
            this.splitContainerMain.Panel1.Controls.Add(this.menuStripMain);
            // 
            // splitContainerMain.Panel2
            // 
            this.splitContainerMain.Panel2.Controls.Add(this.tabControlMain);
            this.splitContainerMain.Size = new System.Drawing.Size(417, 223);
            this.splitContainerMain.SplitterDistance = 77;
            this.splitContainerMain.TabIndex = 0;
            // 
            // textBoxBalance
            // 
            this.textBoxBalance.ContextMenuStrip = this.contextMenuStripBalance;
            this.textBoxBalance.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxBalance.Font = new System.Drawing.Font("Microsoft Sans Serif", 27.75F, System.Drawing.FontStyle.Bold);
            this.textBoxBalance.Location = new System.Drawing.Point(0, 24);
            this.textBoxBalance.Name = "textBoxBalance";
            this.textBoxBalance.ReadOnly = true;
            this.textBoxBalance.Size = new System.Drawing.Size(417, 49);
            this.textBoxBalance.TabIndex = 1;
            this.textBoxBalance.Text = "567.1234 BTC";
            this.textBoxBalance.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.textBoxBalance.Click += new System.EventHandler(this.textBoxBalance_Click);
            this.textBoxBalance.TextChanged += new System.EventHandler(this.textBoxBalance_TextChanged);
            // 
            // contextMenuStripBalance
            // 
            this.contextMenuStripBalance.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.syncWalletToolStripMenuItem2});
            this.contextMenuStripBalance.Name = "contextMenuStripBalance";
            this.contextMenuStripBalance.Size = new System.Drawing.Size(134, 26);
            // 
            // syncWalletToolStripMenuItem2
            // 
            this.syncWalletToolStripMenuItem2.Name = "syncWalletToolStripMenuItem2";
            this.syncWalletToolStripMenuItem2.Size = new System.Drawing.Size(133, 22);
            this.syncWalletToolStripMenuItem2.Text = "Sync wallet";
            this.syncWalletToolStripMenuItem2.Click += new System.EventHandler(this.syncWalletToolStripMenuItem_Click);
            // 
            // menuStripMain
            // 
            this.menuStripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.viewToolStripMenuItem,
            this.actionsToolStripMenuItem});
            this.menuStripMain.Location = new System.Drawing.Point(0, 0);
            this.menuStripMain.Name = "menuStripMain";
            this.menuStripMain.Size = new System.Drawing.Size(417, 24);
            this.menuStripMain.TabIndex = 0;
            this.menuStripMain.Text = "menuStrip1";
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.receiveAddressesToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewToolStripMenuItem.Text = "View";
            // 
            // receiveAddressesToolStripMenuItem
            // 
            this.receiveAddressesToolStripMenuItem.Name = "receiveAddressesToolStripMenuItem";
            this.receiveAddressesToolStripMenuItem.Size = new System.Drawing.Size(168, 22);
            this.receiveAddressesToolStripMenuItem.Text = "Receive addresses";
            this.receiveAddressesToolStripMenuItem.Click += new System.EventHandler(this.receiveAddressesToolStripMenuItem_Click);
            // 
            // actionsToolStripMenuItem
            // 
            this.actionsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.syncWalletToolStripMenuItem1});
            this.actionsToolStripMenuItem.Name = "actionsToolStripMenuItem";
            this.actionsToolStripMenuItem.Size = new System.Drawing.Size(59, 20);
            this.actionsToolStripMenuItem.Text = "Actions";
            // 
            // syncWalletToolStripMenuItem1
            // 
            this.syncWalletToolStripMenuItem1.Name = "syncWalletToolStripMenuItem1";
            this.syncWalletToolStripMenuItem1.Size = new System.Drawing.Size(133, 22);
            this.syncWalletToolStripMenuItem1.Text = "Sync wallet";
            this.syncWalletToolStripMenuItem1.Click += new System.EventHandler(this.syncWalletToolStripMenuItem1_Click);
            // 
            // tabControlMain
            // 
            this.tabControlMain.Controls.Add(this.tabPageReceive);
            this.tabControlMain.Controls.Add(this.tabPageSend);
            this.tabControlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlMain.Location = new System.Drawing.Point(0, 0);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.Size = new System.Drawing.Size(417, 142);
            this.tabControlMain.TabIndex = 0;
            // 
            // tabPageReceive
            // 
            this.tabPageReceive.Controls.Add(this.buttonGenerateNewAddress);
            this.tabPageReceive.Controls.Add(this.textBoxRecieveAddress);
            this.tabPageReceive.Location = new System.Drawing.Point(4, 22);
            this.tabPageReceive.Name = "tabPageReceive";
            this.tabPageReceive.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageReceive.Size = new System.Drawing.Size(409, 116);
            this.tabPageReceive.TabIndex = 0;
            this.tabPageReceive.Text = "Receive";
            this.tabPageReceive.UseVisualStyleBackColor = true;
            // 
            // buttonGenerateNewAddress
            // 
            this.buttonGenerateNewAddress.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonGenerateNewAddress.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.buttonGenerateNewAddress.Location = new System.Drawing.Point(8, 60);
            this.buttonGenerateNewAddress.Name = "buttonGenerateNewAddress";
            this.buttonGenerateNewAddress.Size = new System.Drawing.Size(390, 50);
            this.buttonGenerateNewAddress.TabIndex = 1;
            this.buttonGenerateNewAddress.Text = "GENERATE NEW ADDRESS";
            this.buttonGenerateNewAddress.UseVisualStyleBackColor = true;
            this.buttonGenerateNewAddress.Click += new System.EventHandler(this.buttonGenerateNewAddress_Click);
            // 
            // textBoxRecieveAddress
            // 
            this.textBoxRecieveAddress.ContextMenuStrip = this.contextMenuStripAddress;
            this.textBoxRecieveAddress.Location = new System.Drawing.Point(9, 6);
            this.textBoxRecieveAddress.Name = "textBoxRecieveAddress";
            this.textBoxRecieveAddress.ReadOnly = true;
            this.textBoxRecieveAddress.Size = new System.Drawing.Size(390, 20);
            this.textBoxRecieveAddress.TabIndex = 0;
            this.textBoxRecieveAddress.Text = "1E6aG3JAwwvJAUvAUGLF987TVbrCYS1oKa";
            this.textBoxRecieveAddress.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.textBoxRecieveAddress.Click += new System.EventHandler(this.textBoxRecieveAddress_Click);
            this.textBoxRecieveAddress.TextChanged += new System.EventHandler(this.textBoxRecieveAddress_TextChanged);
            // 
            // contextMenuStripAddress
            // 
            this.contextMenuStripAddress.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyToolStripMenuItem,
            this.viewOnBlockchainToolStripMenuItem,
            this.viewReceiveAddressesToolStripMenuItem});
            this.contextMenuStripAddress.Name = "contextMenuStripAddress";
            this.contextMenuStripAddress.Size = new System.Drawing.Size(194, 70);
            // 
            // copyToolStripMenuItem
            // 
            this.copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            this.copyToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.copyToolStripMenuItem.Text = "Copy";
            this.copyToolStripMenuItem.Click += new System.EventHandler(this.copyToolStripMenuItem_Click);
            // 
            // viewOnBlockchainToolStripMenuItem
            // 
            this.viewOnBlockchainToolStripMenuItem.Name = "viewOnBlockchainToolStripMenuItem";
            this.viewOnBlockchainToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.viewOnBlockchainToolStripMenuItem.Text = "View on blockchain";
            this.viewOnBlockchainToolStripMenuItem.Click += new System.EventHandler(this.viewOnBlockchainToolStripMenuItem_Click);
            // 
            // viewReceiveAddressesToolStripMenuItem
            // 
            this.viewReceiveAddressesToolStripMenuItem.Name = "viewReceiveAddressesToolStripMenuItem";
            this.viewReceiveAddressesToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.viewReceiveAddressesToolStripMenuItem.Text = "View receive addresses";
            this.viewReceiveAddressesToolStripMenuItem.Click += new System.EventHandler(this.viewReceiveAddressesToolStripMenuItem_Click);
            // 
            // tabPageSend
            // 
            this.tabPageSend.Controls.Add(this.labelBtc);
            this.tabPageSend.Controls.Add(this.buttonAll);
            this.tabPageSend.Controls.Add(this.textBoxBtc);
            this.tabPageSend.Controls.Add(this.textBoxSendAddress);
            this.tabPageSend.Controls.Add(this.buttonSend);
            this.tabPageSend.Location = new System.Drawing.Point(4, 22);
            this.tabPageSend.Name = "tabPageSend";
            this.tabPageSend.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageSend.Size = new System.Drawing.Size(409, 116);
            this.tabPageSend.TabIndex = 1;
            this.tabPageSend.Text = "Send";
            this.tabPageSend.UseVisualStyleBackColor = true;
            // 
            // labelBtc
            // 
            this.labelBtc.AutoSize = true;
            this.labelBtc.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.labelBtc.Location = new System.Drawing.Point(144, 32);
            this.labelBtc.Name = "labelBtc";
            this.labelBtc.Size = new System.Drawing.Size(40, 20);
            this.labelBtc.TabIndex = 10;
            this.labelBtc.Text = "BTC";
            // 
            // buttonAll
            // 
            this.buttonAll.Location = new System.Drawing.Point(9, 32);
            this.buttonAll.Name = "buttonAll";
            this.buttonAll.Size = new System.Drawing.Size(44, 23);
            this.buttonAll.TabIndex = 6;
            this.buttonAll.Text = "ALL";
            this.buttonAll.UseVisualStyleBackColor = true;
            this.buttonAll.Click += new System.EventHandler(this.buttonAll_Click);
            // 
            // textBoxBtc
            // 
            this.textBoxBtc.Location = new System.Drawing.Point(59, 32);
            this.textBoxBtc.Name = "textBoxBtc";
            this.textBoxBtc.Size = new System.Drawing.Size(85, 20);
            this.textBoxBtc.TabIndex = 5;
            this.textBoxBtc.Text = "1.2345";
            this.textBoxBtc.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.textBoxBtc.TextChanged += new System.EventHandler(this.textBoxBtc_TextChanged);
            this.textBoxBtc.Enter += new System.EventHandler(this.textBoxBtc_Enter);
            this.textBoxBtc.Leave += new System.EventHandler(this.textBoxBtc_Leave);
            // 
            // textBoxSendAddress
            // 
            this.textBoxSendAddress.BackColor = System.Drawing.SystemColors.Window;
            this.textBoxSendAddress.Location = new System.Drawing.Point(9, 6);
            this.textBoxSendAddress.Name = "textBoxSendAddress";
            this.textBoxSendAddress.Size = new System.Drawing.Size(390, 20);
            this.textBoxSendAddress.TabIndex = 4;
            this.textBoxSendAddress.Text = "1E6aG3JAwwvJAUvAUGLF987TVbrCYS1oKa";
            this.textBoxSendAddress.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.textBoxSendAddress.TextChanged += new System.EventHandler(this.textBoxSendAddress_TextChanged);
            this.textBoxSendAddress.Enter += new System.EventHandler(this.textBoxSendAddress_Enter);
            this.textBoxSendAddress.Leave += new System.EventHandler(this.textBoxSendAddress_Leave);
            // 
            // buttonSend
            // 
            this.buttonSend.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F);
            this.buttonSend.Location = new System.Drawing.Point(8, 60);
            this.buttonSend.Name = "buttonSend";
            this.buttonSend.Size = new System.Drawing.Size(390, 50);
            this.buttonSend.TabIndex = 3;
            this.buttonSend.Text = "SEND";
            this.buttonSend.UseVisualStyleBackColor = true;
            this.buttonSend.EnabledChanged += new System.EventHandler(this.buttonSend_EnabledChanged);
            this.buttonSend.Click += new System.EventHandler(this.buttonSend_Click);
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 222);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(413, 22);
            this.statusStrip.TabIndex = 1;
            // 
            // toolStripStatusLabel
            // 
            this.toolStripStatusLabel.Name = "toolStripStatusLabel";
            this.toolStripStatusLabel.Size = new System.Drawing.Size(39, 17);
            this.toolStripStatusLabel.Text = "Status";
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(413, 244);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.splitContainerMain);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStripMain;
            this.MaximizeBox = false;
            this.Name = "FormMain";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Hidden Wallet";
            this.Shown += new System.EventHandler(this.MainForm_Shown);
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel1.PerformLayout();
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            this.contextMenuStripBalance.ResumeLayout(false);
            this.menuStripMain.ResumeLayout(false);
            this.menuStripMain.PerformLayout();
            this.tabControlMain.ResumeLayout(false);
            this.tabPageReceive.ResumeLayout(false);
            this.tabPageReceive.PerformLayout();
            this.contextMenuStripAddress.ResumeLayout(false);
            this.tabPageSend.ResumeLayout(false);
            this.tabPageSend.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.MenuStrip menuStripMain;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem receiveAddressesToolStripMenuItem;
        private System.Windows.Forms.TabControl tabControlMain;
        private System.Windows.Forms.TabPage tabPageReceive;
        private System.Windows.Forms.TabPage tabPageSend;
        private System.Windows.Forms.TextBox textBoxBalance;
        private System.Windows.Forms.Button buttonGenerateNewAddress;
        private System.Windows.Forms.TextBox textBoxRecieveAddress;
        private CueTextBox textBoxSendAddress;
        private System.Windows.Forms.Button buttonSend;
        private System.Windows.Forms.Button buttonAll;
        private CueTextBox textBoxBtc;
        private System.Windows.Forms.Label labelBtc;
        private ToolStripMenuItem actionsToolStripMenuItem;
        private ToolStripMenuItem syncWalletToolStripMenuItem1;
        private ContextMenuStrip contextMenuStripBalance;
        private ToolStripMenuItem syncWalletToolStripMenuItem2;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel toolStripStatusLabel;
        private ContextMenuStrip contextMenuStripAddress;
        private ToolStripMenuItem copyToolStripMenuItem;
        private ToolStripMenuItem viewOnBlockchainToolStripMenuItem;
        private ToolStripMenuItem viewReceiveAddressesToolStripMenuItem;
    }
}

