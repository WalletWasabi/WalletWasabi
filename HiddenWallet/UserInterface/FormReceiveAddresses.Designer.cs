namespace HiddenWallet.UserInterface
{
    partial class FormReceiveAddresses
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormReceiveAddresses));
            this.dataGridViewReceiveAddresses = new System.Windows.Forms.DataGridView();
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewOnBlockchainToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewReceiveAddresses)).BeginInit();
            this.contextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridViewReceiveAddresses
            // 
            this.dataGridViewReceiveAddresses.AllowUserToAddRows = false;
            this.dataGridViewReceiveAddresses.AllowUserToDeleteRows = false;
            this.dataGridViewReceiveAddresses.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewReceiveAddresses.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.dataGridViewReceiveAddresses.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            this.dataGridViewReceiveAddresses.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewReceiveAddresses.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewReceiveAddresses.Location = new System.Drawing.Point(0, 0);
            this.dataGridViewReceiveAddresses.MultiSelect = false;
            this.dataGridViewReceiveAddresses.Name = "dataGridViewReceiveAddresses";
            this.dataGridViewReceiveAddresses.ReadOnly = true;
            this.dataGridViewReceiveAddresses.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dataGridViewReceiveAddresses.Size = new System.Drawing.Size(413, 261);
            this.dataGridViewReceiveAddresses.TabIndex = 0;
            this.dataGridViewReceiveAddresses.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dataGridViewReceiveAddresses_CellMouseDown);
            this.dataGridViewReceiveAddresses.RowPostPaint += new System.Windows.Forms.DataGridViewRowPostPaintEventHandler(this.dataGridViewReceiveAddresses_RowPostPaint);
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyToolStripMenuItem,
            this.viewOnBlockchainToolStripMenuItem});
            this.contextMenuStrip.Name = "contextMenuStrip";
            this.contextMenuStrip.Size = new System.Drawing.Size(178, 48);
            // 
            // copyToolStripMenuItem
            // 
            this.copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            this.copyToolStripMenuItem.Size = new System.Drawing.Size(177, 22);
            this.copyToolStripMenuItem.Text = "Copy";
            this.copyToolStripMenuItem.Click += new System.EventHandler(this.copyToolStripMenuItem_Click);
            // 
            // viewOnBlockchainToolStripMenuItem
            // 
            this.viewOnBlockchainToolStripMenuItem.Name = "viewOnBlockchainToolStripMenuItem";
            this.viewOnBlockchainToolStripMenuItem.Size = new System.Drawing.Size(177, 22);
            this.viewOnBlockchainToolStripMenuItem.Text = "View on blockchain";
            this.viewOnBlockchainToolStripMenuItem.Click += new System.EventHandler(this.viewOnBlockchainToolStripMenuItem_Click);
            // 
            // FormReceiveAddresses
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(413, 261);
            this.Controls.Add(this.dataGridViewReceiveAddresses);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximumSize = new System.Drawing.Size(429, 9999);
            this.MinimumSize = new System.Drawing.Size(429, 38);
            this.Name = "FormReceiveAddresses";
            this.Text = "HiddenWallet: Receive addresses";
            this.Load += new System.EventHandler(this.FormAddresses_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewReceiveAddresses)).EndInit();
            this.contextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridViewReceiveAddresses;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewOnBlockchainToolStripMenuItem;
    }
}