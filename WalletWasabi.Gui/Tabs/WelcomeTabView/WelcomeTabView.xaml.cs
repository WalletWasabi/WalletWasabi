using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;

namespace WalletWasabi.Gui.Tabs.WelcomeTab
{
	internal class WelcomeTabView : UserControl
	{
		public WelcomeTabView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
