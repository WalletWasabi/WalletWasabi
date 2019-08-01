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

			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				HasSystemDecorations = true;

				// This will need implementing properly once this is supported by avalonia itself.
				var color = (ColorTheme.CurrentTheme.Background as SolidColorBrush).Color;
				(PlatformImpl as WindowImpl).SetTitleBarColor(color);
			}
		}
	}
}
