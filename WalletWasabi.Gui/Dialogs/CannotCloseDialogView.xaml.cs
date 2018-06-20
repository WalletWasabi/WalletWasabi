using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Dialogs
{
	internal class CannotCloseDialogView : UserControl
	{
		public CannotCloseDialogView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
