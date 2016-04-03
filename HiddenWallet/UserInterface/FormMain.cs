// The user interface is built to display and manipulate the data.
// UI Forms always get organized by functional unit namespace with an 
// additional folder for shard forms and one for custom controls.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using HiddenWallet.Properties;
using HiddenWallet.Services;
using HiddenWallet.UserInterface.Controls;
using System.Runtime.InteropServices;
using NBitcoin;

namespace HiddenWallet.UserInterface
{
    internal partial class FormMain : Form
    {
        private string _bitcoinSendAddress;
        private decimal _amountBtc;

        [DllImport("user32.dll")]
        static extern bool HideCaret(IntPtr hWnd);

        internal FormMain()
        {
            InitializeComponent();
        }

        private string AskPassword()
        {
            var password = "";
            var noGo = true;
            while (noGo)
            {
                try
                {
                    password = "";
                    var createWallet = !WalletServices.WalletExists();
                    if (createWallet)
                    {
                        InputDialog.Show(ref password, Resources.FormMain_AskPassword_Create_your_wallet,
                            Resources.FormMain_AskPassword_Choose_a_password);
                        WalletServices.CreateWallet(password);
                        MessageBox.Show(this,
                            Resources.Please_backup_your_wallet_file + Environment.NewLine +
                            WalletServices.GetPathWalletFile(),
                            Resources.Wallet_created, MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    InputDialog.Show(ref password, Resources.FormMain_AskPassword_Open_your_wallet,
                        Resources.InputDialog_Show_Password);
                    WalletServices.LoadWallet(password);
                    SyncWallet();
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

            return password;
        }

        private void receiveAddressesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = new FormReceiveAddresses();
            form.Show();
        }

        private void buttonGenerateNewAddress_Click(object sender, EventArgs e)
        {
            contextMenuStripAddress.Enabled = true;
            textBoxRecieveAddress.Text = WalletServices.GenerateKey();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            textBoxSendAddress.Text = "";
            SetCueToTextBoxSendAddress();
            textBoxBtc.Text = "";
            SetCueToTextBoxBtc();

            contextMenuStripAddress.Enabled = false;

            textBoxRecieveAddress.Text = "";
            textBoxBalance.Text = "";
            textBoxSendAddress.Text = "";

            AskPassword();
            
            buttonSend.Enabled = false;

            RefreshBalance();
            DataRepository.Main.Wallet.ThrowEvent += (sender2, args) => { RefreshBalance(); };

            HideCaretClearSelection(textBoxBalance);
            HideCaretClearSelection(textBoxRecieveAddress);
        }

        private void SetCueToTextBoxSendAddress()
        {
            textBoxSendAddress.Cue = @"1E6aG3JAwwvJAUvAUGLF987TVbrCYS1oKa";
        }
        private void ClearCueToTextBoxSendAddress()
        {
            textBoxSendAddress.Cue = @"";
        }
        private void SetCueToTextBoxBtc()
        {
            textBoxBtc.Cue = @"21.1";
        }
        private void ClearCueToTextBoxBtc()
        {
            textBoxBtc.Cue = @"";
        }

        private void RefreshBalance()
        {
            var balance = DataRepository.Main.Wallet.Balance;
            textBoxBalance.Text = string.Format(CultureInfo.InvariantCulture, "{0:0.####} BTC", balance);
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

        private void textBoxSendAddress_TextChanged(object sender, EventArgs e)
        {
            ValidateTextBoxSendAddress((TextBox)sender);
        }

        private void ValidateTextBoxSendAddress(Control textBoxBase)
        {
            var address = textBoxBase.Text.Trim();

            if (address == "")
            {
                textBoxBase.BackColor = SystemColors.Window;
                buttonSend.Enabled = false;
                return;
            }

            try
            {
                _bitcoinSendAddress = new BitcoinPubKeyAddress(address, DataRepository.Main.Network).ToString();
                textBoxBase.BackColor = Color.PaleGreen;
                buttonSend.Enabled = true;
            }
            catch
            {
                textBoxBase.BackColor = Color.PaleVioletRed;
                buttonSend.Enabled = false;
            }
        }

        private void ValidateBtcTextBox(Control textBoxBase)
        {
            var btcAmountString = textBoxBase.Text.Trim();

            if (btcAmountString == "")
            {
                textBoxBase.BackColor = SystemColors.Window;
                buttonSend.Enabled = false;
                return;
            }

            try
            {
                btcAmountString = btcAmountString.Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator);
                var btcAmount = decimal.Parse(btcAmountString, NumberStyles.Any, CultureInfo.InvariantCulture);

                if (btcAmount <= DataRepository.Main.Wallet.Balance && btcAmount > 0)
                {
                    _amountBtc = btcAmount;
                    textBoxBase.BackColor = Color.PaleGreen;
                    buttonSend.Enabled = true;
                    return;
                }
                throw new Exception("NotEnoughFunds");
            }
            catch
            {
                textBoxBase.BackColor = Color.PaleVioletRed;
                buttonSend.Enabled = false;
            }
        }

        private void viewOnBlockchainToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(DataRepository.Main.Network == Network.Main ? "https://blockchain.info/address/" : "http://tbtc.blockr.io/address/info/" + textBoxRecieveAddress.Text);
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(textBoxRecieveAddress.Text);
        }

        private void viewReceiveAddressesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = new FormReceiveAddresses();
            form.Show();
        }

        private void textBoxSendAddress_Enter(object sender, EventArgs e)
        {
            ClearCueToTextBoxSendAddress();
        }

        private void textBoxSendAddress_Leave(object sender, EventArgs e)
        {
            SetCueToTextBoxSendAddress();
        }

        private void textBoxBtc_Leave(object sender, EventArgs e)
        {
            SetCueToTextBoxBtc();
        }

        private void textBoxBtc_Enter(object sender, EventArgs e)
        {
            ClearCueToTextBoxBtc();
        }

        private void buttonAll_Click(object sender, EventArgs e)
        {
            var balance = DataRepository.Main.Wallet.Balance;
            textBoxBtc.Text = string.Format(CultureInfo.InvariantCulture, "{0}", balance);
        }

        private void textBoxBtc_TextChanged(object sender, EventArgs e)
        {
            ValidateBtcTextBox((TextBox)sender);
        }

        private void buttonSend_EnabledChanged(object sender, EventArgs e)
        {
            if(textBoxBtc.BackColor == Color.PaleGreen && textBoxSendAddress.BackColor == Color.PaleGreen)
            {
                buttonSend.Enabled = true;
            }
            else
            {
                buttonSend.Enabled = false;
            }
        }

        private void buttonSend_Click(object sender, EventArgs e)
        {
            DataRepository.Main.Wallet.Send(_bitcoinSendAddress, _amountBtc);
        }
    }
}