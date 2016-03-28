using System;
using System.Drawing;
using System.Windows.Forms;
using HiddenWallet.DataRepository;
using NBitcoin.SPV;

// ReSharper disable UnusedMember.Local

namespace HiddenWallet.UserInterface
{
    public partial class FormReceiveAddresses : Form
    {
        public FormReceiveAddresses()
        {
            InitializeComponent();
        }

        private void FormAddresses_Load(object sender, EventArgs e)
        {
            Main.Wallet.Update();
            dataGridViewReceiveAddresses.DataSource = new BindingSource(Main.Wallet.NotUsedAddresses, null);
        }

        // http://stackoverflow.com/a/12840794/2061103
        private void dataGridViewReceiveAddresses_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            var grid = sender as DataGridView;
            var rowIdx = (e.RowIndex + 1).ToString();

            var centerFormat = new StringFormat
            {
                // right alignment might actually make more sense for numbers
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            if (grid == null) return;
            var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
            e.Graphics.DrawString(rowIdx, Font, SystemBrushes.ControlText, headerBounds, centerFormat);
        }

        private void dataGridViewReceiveAddresses_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Add this
                dataGridViewReceiveAddresses.CurrentCell =
                    dataGridViewReceiveAddresses.Rows[e.RowIndex].Cells[e.ColumnIndex];
                // Can leave these here - doesn't hurt
                dataGridViewReceiveAddresses.Rows[e.RowIndex].Selected = true;
                dataGridViewReceiveAddresses.Focus();
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(dataGridViewReceiveAddresses.SelectedCells[0].Value.ToString());
        }

        // http://stackoverflow.com/a/2140908/2061103
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            switch (m.Msg)
            {
                case 0x84: //WM_NCHITTEST
                    var result = (HitTest) m.Result.ToInt32();
                    if (result == HitTest.Left || result == HitTest.Right)
                        m.Result = new IntPtr((int) HitTest.Caption);
                    if (result == HitTest.TopLeft || result == HitTest.TopRight)
                        m.Result = new IntPtr((int) HitTest.Top);
                    if (result == HitTest.BottomLeft || result == HitTest.BottomRight)
                        m.Result = new IntPtr((int) HitTest.Bottom);

                    break;
            }
        }

        private enum HitTest
        {
            Caption = 2,
            Transparent = -1,
            Nowhere = 0,
            Client = 1,
            Left = 10,
            Right = 11,
            Top = 12,
            TopLeft = 13,
            TopRight = 14,
            Bottom = 15,
            BottomLeft = 16,
            BottomRight = 17,
            Border = 18
        }
    }
}