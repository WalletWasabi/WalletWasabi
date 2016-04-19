using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Windows.Forms;
using HiddenWallet.Properties;
using HiddenWallet.Services;

namespace HiddenWallet.UserInterface
{
    public partial class FormSettings : Form
    {
        private readonly List<object> _unchangedSettings = new List<object>();

        public FormSettings()
        {
            InitializeComponent();
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

            Main.LoadSettings(true);
        }

        private void Cancel()
        {
            Settings.Default.Reload();
            Close();
        }

        private void FormSettings_Shown(object sender, EventArgs e)
        {
            comboBoxNetwork.Items.Add("Test");
            comboBoxNetwork.Items.Add("Main");

            var network = Settings.Default.Network;
            comboBoxNetwork.SelectedIndex = comboBoxNetwork.Items.IndexOf(network);
            if (comboBoxNetwork.SelectedIndex == -1)
                throw new Exception("WrongNetworkSetting");

            Settings.Default.PropertyChanged += SettingChanged;

            foreach (
                var propVal in
                    from SettingsPropertyValue value in Settings.Default.PropertyValues select value.PropertyValue)
            {
                _unchangedSettings.Add(propVal);
            }
        }

        private void SettingChanged(object sender, PropertyChangedEventArgs e)
        {
            var changed = false;

            var i = 0;
            foreach (SettingsPropertyValue value in Settings.Default.PropertyValues)
            {
                if (value.PropertyValue != _unchangedSettings[i])
                    changed = true;
                i++;
            }

            buttonApply.Enabled = changed;
        }

        private void comboBoxNetwork_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Default.Network = comboBoxNetwork.SelectedItem.ToString();
        }

        private void FormSettings_Load(object sender, EventArgs e)
        {
            ControlBox = false;
            buttonApply.Enabled = false;
        }
    }
}