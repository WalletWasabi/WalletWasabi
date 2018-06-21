using AvalonStudio.Extensibility;
using AvalonStudio.MVVM;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Text;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	[Export(typeof(IExtension))]
	[ExportToolControl]
	[Shared]
	public class WalletExplorerViewModel : ToolViewModel, IExtension
	{
		public override Location DefaultLocation => Location.Right;

		public WalletExplorerViewModel()
		{
			Title = "Wallet Explorer";
		}
	}
}
