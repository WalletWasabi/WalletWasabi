using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvalonStudio.Shell.Controls;

namespace WalletWasabi.Gui.ManagedDialogs
{
	class ManagedFileDialog : MetroWindow
	{
		public ManagedFileDialog()
		{
			AvaloniaXamlLoader.Load(this);
#if DEBUG
			this.AttachDevTools();
#endif
		}
	}
}
