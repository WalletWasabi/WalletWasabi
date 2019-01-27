using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Tabs.EncryptionManager
{
	internal class EncryptMessageView : UserControl
	{
		public EncryptMessageView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
