// I always create a code module that contains helper classes.These
// may be extensions on system items, standard validation tools,
// regular expressions or custom built items.  

using System;
using System.Drawing;
using System.Windows.Forms;

namespace HiddenWallet.Helpers
{
    internal static class Main
    {
        internal static char GetRandomCharacter(string chars)
        {
            var rng = new Random();
            var index = rng.Next(chars.Length);
            return chars[index];
        }

        internal static DialogResult ShowInputDialog(ref string input)
        {
            var size = new Size(200, 70);
            var inputBox = new Form();

            inputBox.FormBorderStyle = FormBorderStyle.FixedDialog;
            inputBox.ClientSize = size;
            inputBox.Text = "Name";

            var textBox = new TextBox();
            textBox.Size = new Size(size.Width - 10, 23);
            textBox.Location = new Point(5, 5);
            textBox.Text = input;
            inputBox.Controls.Add(textBox);

            var okButton = new Button();
            okButton.DialogResult = DialogResult.OK;
            okButton.Name = "okButton";
            okButton.Size = new Size(75, 23);
            okButton.Text = "&OK";
            okButton.Location = new Point(size.Width - 80 - 80, 39);
            inputBox.Controls.Add(okButton);

            var cancelButton = new Button();
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new Size(75, 23);
            cancelButton.Text = "&Cancel";
            cancelButton.Location = new Point(size.Width - 80, 39);
            inputBox.Controls.Add(cancelButton);

            inputBox.AcceptButton = okButton;
            inputBox.CancelButton = cancelButton;

            var result = inputBox.ShowDialog();
            input = textBox.Text;
            return result;
        }
    }
}