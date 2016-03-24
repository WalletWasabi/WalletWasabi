// The user interface is built to display and manipulate the data.
// UI Forms always get organized by functional unit namespace with an 
// additional folder for shard forms and one for custom controls.

using System;
using System.Windows.Forms;
using HiddenWallet.Services;
using HiddenWallet.UserInterface.Controls;

namespace HiddenWallet.UserInterface
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            var password = "";
            InputDialog.Show(ref password);
            Main.CreateWallet(password);
        }
    }
}