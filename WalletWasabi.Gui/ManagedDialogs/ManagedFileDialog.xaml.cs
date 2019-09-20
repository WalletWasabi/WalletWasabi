using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Native;
using AvalonStudio.Extensibility.Theme;
using AvalonStudio.Shell.Controls;

namespace WalletWasabi.Gui.ManagedDialogs
{
	internal class ManagedFileDialog : MetroWindow
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
