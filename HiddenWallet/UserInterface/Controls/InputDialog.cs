using System.Drawing;
using System.Windows.Forms;
using HiddenWallet.Properties;

namespace HiddenWallet.UserInterface.Controls
{
    internal static class InputDialog
    {
        // http://stackoverflow.com/a/17546909/2061103
        internal static DialogResult Show(ref string input)
        {
            var size = new Size(200, 70);
            var inputBox = new Form
            {
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ClientSize = size,
                Text = Resources.InputDialog_Show_Password,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ControlBox = false
            };

            var textBox = new TextBox
            {
                Size = new Size(size.Width - 10, 23),
                Location = new Point(5, 10),
                Text = input,
                UseSystemPasswordChar = true
            };
            inputBox.Controls.Add(textBox);

            var okButton = new Button
            {
                DialogResult = DialogResult.OK,
                Name = "okButton",
                Size = new Size(75, 23),
                Text = Resources.InputDialog_Show__Ok,
                Location = new Point(size.Width - 60 - 80, 40)
            };
            inputBox.Controls.Add(okButton);

            inputBox.AcceptButton = okButton;

            var result = inputBox.ShowDialog();
            input = textBox.Text;
            return result;
        }
    }
}