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
            Enabled = false;
            AskPassword();
            Enabled = true;

            RefreshBalance();
            DataRepository.Main.Wallet.ThrowEvent += (sender2, args) => { RefreshBalance(); };
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            textBoxRecieveAddress.Text = "";
            textBoxBalance.Text = "";
        }

        private void RefreshBalance()
        {
            textBoxBalance.Text = DataRepository.Main.Wallet.Balance.ToString(CultureInfo.InvariantCulture) + @" BTC";
        }

        private void textBoxBalance_TextChanged(object sender, EventArgs e)
        {
            HideCaret(textBoxBalance.Handle);
        }

        private void textBoxRecieveAddress_TextChanged(object sender, EventArgs e)
        {
            HideCaret(textBoxRecieveAddress.Handle);
        }

        private void textBoxRecieveAddress_Enter(object sender, EventArgs e)
        {
            // Kick off SelectAll asyncronously so that it occurs after Click
            BeginInvoke((Action)delegate
            {
                textBoxRecieveAddress.SelectAll();
            });
        }
    }
}