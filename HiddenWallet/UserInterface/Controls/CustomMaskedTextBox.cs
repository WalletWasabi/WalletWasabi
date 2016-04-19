using System;
using System.Text;
using System.Windows.Forms;

namespace HiddenWallet.UserInterface.Controls
{
    public partial class CustomMaskedTextBox : UserControl
    {
        private readonly StringBuilder _maskedTextBuilder = new StringBuilder();
        private readonly Random _rnd = new Random();
        private readonly StringBuilder _userTextBuilder = new StringBuilder();
        private bool _disableTextChangedEvent;

        public CustomMaskedTextBox()
        {
            InitializeComponent();
        }

        public string UserText => _userTextBuilder.ToString();
        public string MaskCharacters { get; set; }

        private void richTextBoxMasked_TextChanged(object sender, EventArgs e)
        {
            if (_disableTextChangedEvent) return;
            var maskedText = richTextBoxMasked.Text;

            //handling usertext
            //get the last character
            if (maskedText.Length > 0)
            {
                var newChar = maskedText.Substring(maskedText.Length - 1, 1);
                _userTextBuilder.Append(newChar);
            }
            //check length and trim
            while (maskedText.Length < _userTextBuilder.Length)
            {
                _userTextBuilder.Remove(_userTextBuilder.Length - 1, 1);
            }
            //handling maskedtext
            while (_maskedTextBuilder.Length < maskedText.Length)
                _maskedTextBuilder.Append(MaskCharacters.Substring(_rnd.Next(0, MaskCharacters.Length), 1));
            while (_maskedTextBuilder.Length > maskedText.Length)
                _maskedTextBuilder.Remove(_maskedTextBuilder.Length - 1, 1);
            _disableTextChangedEvent = true;
            richTextBoxMasked.Text = _maskedTextBuilder.ToString();
            _disableTextChangedEvent = false;
        }

        private void richTextBoxMasked_SelectionChanged(object sender, EventArgs e)
        {
            richTextBoxMasked.SelectionStart = richTextBoxMasked.TextLength;
            richTextBoxMasked.SelectionLength = 0;
        }
    }
}