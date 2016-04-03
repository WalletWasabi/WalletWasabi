using System.Drawing;
using System.Windows.Forms;
using HiddenWallet.Properties;

namespace HiddenWallet.UserInterface.Controls
{
    internal static class InputDialog
    {
        // http://stackoverflow.com/a/17546909/2061103
        internal static DialogResult Show(ref string input,string caption, string message)
        {
            var size = new Size(300, 100);
            var inputBox = new Form
            {
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ClientSize = size,
                Text = caption,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ControlBox = false
            };
            var flp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0,10,0,0)
            };
            inputBox.Controls.Add(flp);

            var label = new Label
            {
                Text = message,
                Size = new Size(size.Width - 10, 23)
            };
            flp.Controls.Add(label);

            var textBox = new CustomMaskedTextBox
            {
                Size = new Size(size.Width - 10, 23),
                Text = input,
                MaskCharacters = "阪熊奈岡鹿梨阜埼茨栃",
                Anchor = AnchorStyles.None
            };
            flp.Controls.Add(textBox);

            var okButton = new Button
            {
                DialogResult = DialogResult.OK,
                Name = "okButton",
                Size = new Size(75, 23),
                Text = Resources.InputDialog_Show__Ok,
                Anchor = AnchorStyles.None
            };
            flp.Controls.Add(okButton);

            inputBox.AcceptButton = okButton;

            var result = inputBox.ShowDialog();
            input = textBox.UserText;
            return result;
        }
    }
}