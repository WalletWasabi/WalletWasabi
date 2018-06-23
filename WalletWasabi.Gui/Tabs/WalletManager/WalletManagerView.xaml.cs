using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;

namespace WalletWasabi.Gui.Tabs.WalletManager
{
	internal class WalletManagerView : UserControl
	{
		public WalletManagerView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			Initialized += OnInitilized;
			AvaloniaXamlLoader.Load(this);
		}

		void OnInitilized(object sender, EventArgs e)
		{
			Initialized -= OnInitilized;
				var ctx = (WalletManagerViewModel)DataContext;
			if(Directory.Exists(Global.WalletsDir) && Directory.EnumerateFiles(Global.WalletsDir).Any())
			{
				// Load
				ctx.SelectedCategory = ctx.Categories.First(x=>x is LoadWalletViewModel);
			}
			else
			{
				// Generate
				ctx.SelectedCategory = ctx.Categories.First(x=>x is GenerateWalletViewModel);
			}
		}

	}
}
