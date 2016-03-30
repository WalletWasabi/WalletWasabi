// The user interface is built to display and manipulate the data.
// UI Forms always get organized by functional unit namespace with an 
// additional folder for shard forms and one for custom controls.

using System;
using System.Globalization;
using System.Windows.Forms;
using HiddenWallet.Properties;
using HiddenWallet.Services;
using HiddenWallet.UserInterface.Controls;
using System.Runtime.InteropServices;

namespace HiddenWallet.UserInterface
{
    internal partial class FormMain : Form
    {
        [DllImport("user32.dll")]
        static extern bool HideCaret(IntPtr hWnd);

        internal FormMain()
        {
            InitializeComponent();
        }

        private void AskPassword()
        {
            var noGo = true;
            while (noGo)
            {
                try
                {
                    var password = "";
                    InputDialog.Show(ref password);

                    var createWallet = !WalletServices.WalletExists();
                    WalletServices.CreateWallet(password);
                    SyncWallet();
                    if (createWallet)
                        MessageBox.Show(this,
                            Resources.Please_backup_your_wallet_file + Environment.NewLine +
                            WalletServices.GetPathWalletFile(),
                            Resources.Wallet_created, MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                    noGo = false;
                }
                catch (Exception exception)
                {
                    if (exception.Message == "WrongPassword")
                    {
                        var result = MessageBox.Show(this, Resources.Incorrect_password, "",
                            MessageBoxButtons.RetryCancel,
                            MessageBoxIcon.Exclamation);
                        if (result == DialogResult.Retry)
                            noGo = true;
                        else Environment.Exit(0);
                    }
                    else
                    {
                        var result = MessageBox.Show(this, exception.ToString(), Resources.Error,
                            MessageBoxButtons.RetryCancel,
                            MessageBoxIcon.Error);
                        if (result == DialogResult.Retry)
                            noGo = true;
                        else Environment.Exit(0);
                    }
                }
            }
        }

        private void receiveAddressesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = new FormReceiveAddresses();
            form.Show();
        }

        private void buttonGenerateNewAddress_Click(object sender, EventArgs e)
        {
            textBoxRecieveAddress.Text = WalletServices.GenerateKey();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            AskPassword();

            RefreshBalance();
            DataRepository.Main.Wallet.ThrowEvent += (sender2, args) => { RefreshBalance(); };
            HideCaretClearSelection(textBoxBalance);
            HideCaretClearSelection(textBoxRecieveAddress);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            textBoxRecieveAddress.Text = "";
            textBoxBalance.Text = "";
        }

        private void RefreshBalance()
        {
            var balance = DataRepository.Main.Wallet.Balance.ToString(CultureInfo.InvariantCulture);
            textBoxBalance.Text = string.Format("{0} BTC", balance);
            HideCaretClearSelection(textBoxBalance);
        }

        private void textBoxBalance_TextChanged(object sender, EventArgs e)
        {
            HideCaretClearSelection(((TextBox)sender));
        }

        private void textBoxRecieveAddress_TextChanged(object sender, EventArgs e)
        {
            HideCaretClearSelection(((TextBox)sender));
        }

        private void syncWalletToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SyncWallet();
            HideCaretClearSelection(textBoxBalance);
        }

        private void syncWalletToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            SyncWallet();
        }

        private void SyncWallet()
        {
            Enabled = false;
            UpdateStatus(Resources.FormMain_SetProgressBar_Syncing_wallet___);
            DataRepository.Main.Wallet.Sync();
            RefreshBalance();
            UpdateStatus(Resources.FormMain_backgroundWorkerSyncWallet_RunWorkerCompleted_Wallet_is_synced);
            Enabled = true;
        }
        private void textBoxBalance_Click(object sender, EventArgs e)
        {
            HideCaret(((TextBox)sender).Handle);
        }

        private void textBoxRecieveAddress_Click(object sender, EventArgs e)
        {
            HideCaretClearSelection(((TextBox)sender));
            // Kick off SelectAll asyncronously so that it occurs after Click
            if (((TextBox)sender).Text != "")
            {
                BeginInvoke((Action)delegate
                {
                    textBoxRecieveAddress.SelectAll();
                });
            }
        }

        private void UpdateStatus(string message)
        {
            toolStripStatusLabel.Text = message;
            statusStrip.Refresh();
        }

        private static void HideCaretClearSelection(TextBoxBase tb)
        {
            tb.SelectionLength = 0;
            HideCaret(tb.Handle);
        }
    }
}