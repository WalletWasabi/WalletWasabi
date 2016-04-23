using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using HiddenWallet.DataClasses;
using HiddenWallet.Properties;

namespace HiddenWallet.UserInterface
{
    public partial class FormSettings : Form
    {
        private string _unchangedAddressCount;
        private string _unchangedNetwork;
        private string _unchangedWalletFilePath;

        public FormSettings()
        {
            InitializeComponent();
        }

        internal string WalletFilePath
        {
            get { return Settings.Default.WalletFilePath; }
            set
            {
                Settings.Default.WalletFilePath = value;
                textBoxWalletFilePath.Text = value;

                var walletContent = new Main.WalletFileStructure(value);
                var network = walletContent.Network;
                comboBoxNetwork.SelectedIndex = comboBoxNetwork.Items.IndexOf(network);
                if (comboBoxNetwork.SelectedIndex == -1)
                    throw new Exception("WrongNetworkSetting");
                textBoxAddressCount.Text = walletContent.KeyCount;
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            Cancel();
        }

        private void buttonApply_Click(object sender, EventArgs e)
        {
            ApplyRestart();
        }

        private void FormSettings_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ApplyRestart();
            }
            if (e.KeyCode == Keys.Escape)
            {
                Cancel();
            }
        }

        private void ApplyRestart()
        {
            Settings.Default.Save();

            DataRepository.Main.WalletFileContent.Network = comboBoxNetwork.SelectedItem.ToString();
            DataRepository.Main.WalletFileContent.KeyCount = textBoxAddressCount.Text;

            DataRepository.Main.WalletFileContent.Save(WalletFilePath);

            Services.Main.LoadSettings(true);
        }

        private void Cancel()
        {
            Settings.Default.Reload();
            Close();
        }

        private void FormSettings_Load(object sender, EventArgs e)
        {
            comboBoxNetwork.Items.Add("TestNet");
            comboBoxNetwork.Items.Add("MainNet");

            WalletFilePath = WalletFilePath;
                // To fire the set events of the property (should be done an other way, it's ugly)
            var walletContent = new Main.WalletFileStructure(WalletFilePath);
            _unchangedNetwork = walletContent.Network;
            _unchangedAddressCount = walletContent.KeyCount;
            _unchangedWalletFilePath = WalletFilePath;

            ControlBox = false;
            ValidateApply();

            var toolTipDepth = new ToolTip
            {
                AutoPopDelay = 10000,
                InitialDelay = 100,
                ReshowDelay = 100,
                ShowAlways = true
            };
            const string toolTipDepthMessage = "The total number of generated addresses.";
            toolTipDepth.SetToolTip(labelAddressCount, toolTipDepthMessage);
            toolTipDepth.SetToolTip(textBoxAddressCount, toolTipDepthMessage);
        }

        private void buttonWalletFileBrowse_Click(object sender, EventArgs e)
        {
            openFileDialogWalletFile.FileName = WalletFilePath;
            var fullWalletPath = Path.GetFullPath(WalletFilePath);
            openFileDialogWalletFile.InitialDirectory = Path.GetDirectoryName(fullWalletPath);
            openFileDialogWalletFile.Filter = @"Wallet files | *.hid";

            var result = openFileDialogWalletFile.ShowDialog();
            if (result != DialogResult.OK) return;

            var pathApplicationDirectory = Path.GetDirectoryName(Application.ExecutablePath);
            Debug.Assert(pathApplicationDirectory != null, "pathApplicationDirectory != null");
            var pathApplicationDirectoryWithSeparator = pathApplicationDirectory + Path.DirectorySeparatorChar;
            WalletFilePath = openFileDialogWalletFile.FileName.Replace(pathApplicationDirectoryWithSeparator, "");
        }

        private void textBoxWalletFilePath_TextChanged(object sender, EventArgs e)
        {
            textBoxWalletFilePath.SelectionStart = textBoxWalletFilePath.TextLength;
            textBoxWalletFilePath.ScrollToCaret();

            ValidateApply();
        }

        private void comboBoxNetwork_SelectedIndexChanged(object sender, EventArgs e)
        {
            ValidateApply();
        }

        private void textBoxAddressCount_TextChanged(object sender, EventArgs e)
        {
            ValidateApply();
            ValidateTextBoxAddressCount((TextBox) sender);
        }

        private static void ValidateTextBoxAddressCount(Control textBoxBase)
        {
            try
            {
                var count = int.Parse(textBoxBase.Text);
                textBoxBase.BackColor = count < 0 ? Color.PaleVioletRed : Color.PaleGreen;
            }
            catch (Exception)
            {
                textBoxBase.BackColor = Color.PaleVioletRed;
            }
        }

        private void ValidateApply()
        {
            if (textBoxAddressCount.Text == "")
                return;
            buttonApply.Enabled = _unchangedAddressCount != textBoxAddressCount.Text ||
                                  _unchangedNetwork != comboBoxNetwork.SelectedItem.ToString() ||
                                  _unchangedWalletFilePath != WalletFilePath;
        }
    }
}