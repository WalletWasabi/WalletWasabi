using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI;

namespace HiddenWallet.UI
{
	public class WindowConfig : Window
	{
		private VerticalBox _verticalBoxMain;

		private MultilineEntry _multilineEntryConfig;
		private Button _buttonSave;

		public WindowConfig(string title = "Edit your Config.json file", int width = 320, int height = 240)
			: base(title, width, height, hasMenubar: false)
		{
			AllowMargins = true;

			InitializeComponent();
		}

		private void InitializeComponent()
		{
			_verticalBoxMain = new VerticalBox {AllowPadding = true};
			Child = _verticalBoxMain;

			_multilineEntryConfig = new MultilineEntry(false);
			foreach(var line in File.ReadAllLines(ConfigFileSerializer.ConfigFilePath))
			{
				_multilineEntryConfig.Append(line + Environment.NewLine);
			}
			
			_buttonSave = new Button("Save");
			_buttonSave.Click += _buttonSave_Click;

			_verticalBoxMain.Children.Add(_multilineEntryConfig, stretchy: true);
			_verticalBoxMain.Children.Add(_buttonSave);
		}

		private void _buttonSave_Click(object sender, EventArgs e)
		{
			File.WriteAllLines(
				ConfigFileSerializer.ConfigFilePath,
				_multilineEntryConfig.Text.Split(new string[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries));

			Close();
		}
	}
}
