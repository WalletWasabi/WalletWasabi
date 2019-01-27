using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Tabs.EncryptionManager
{
	internal class VerifyMessageView : UserControl
	{
		public VerifyMessageView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
