using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Tabs.EncryptionManager
{
	internal class DecryptMessageView : UserControl
	{
		public DecryptMessageView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
