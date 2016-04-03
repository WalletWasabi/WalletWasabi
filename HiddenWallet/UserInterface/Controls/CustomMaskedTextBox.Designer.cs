namespace HiddenWallet.UserInterface.Controls
{
    partial class CustomMaskedTextBox
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.richTextBoxMasked = new System.Windows.Forms.RichTextBox();
            this.panelBorder = new System.Windows.Forms.Panel();
            this.panelBorder.SuspendLayout();
            this.SuspendLayout();
            // 
            // richTextBoxMasked
            // 
            this.richTextBoxMasked.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBoxMasked.BulletIndent = 5;
            this.richTextBoxMasked.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.richTextBoxMasked.Dock = System.Windows.Forms.DockStyle.Fill;
            this.richTextBoxMasked.Location = new System.Drawing.Point(1, 3);
            this.richTextBoxMasked.MaxLength = 20;
            this.richTextBoxMasked.Multiline = false;
            this.richTextBoxMasked.Name = "richTextBoxMasked";
            this.richTextBoxMasked.Size = new System.Drawing.Size(177, 18);
            this.richTextBoxMasked.TabIndex = 0;
            this.richTextBoxMasked.Text = "";
            this.richTextBoxMasked.WordWrap = false;
            this.richTextBoxMasked.SelectionChanged += new System.EventHandler(this.richTextBoxMasked_SelectionChanged);
            this.richTextBoxMasked.TextChanged += new System.EventHandler(this.richTextBoxMasked_TextChanged);
            // 
            // panelBorder
            // 
            this.panelBorder.BackColor = System.Drawing.Color.White;
            this.panelBorder.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelBorder.Controls.Add(this.richTextBoxMasked);
            this.panelBorder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelBorder.Location = new System.Drawing.Point(0, 0);
            this.panelBorder.Name = "panelBorder";
            this.panelBorder.Padding = new System.Windows.Forms.Padding(1, 3, 1, 1);
            this.panelBorder.Size = new System.Drawing.Size(181, 24);
            this.panelBorder.TabIndex = 1;
            // 
            // CustomMaskedTextBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panelBorder);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Name = "CustomMaskedTextBox";
            this.Size = new System.Drawing.Size(181, 24);
            this.panelBorder.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.RichTextBox richTextBoxMasked;
        private System.Windows.Forms.Panel panelBorder;
    }
}
