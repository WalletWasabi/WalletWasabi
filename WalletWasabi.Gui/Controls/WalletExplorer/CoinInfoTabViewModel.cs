using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reflection;
using Splat;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinInfoTabViewModel : WasabiDocumentTabViewModel
	{
		public CoinInfoTabViewModel(string title, CoinViewModel coin) : base(title)
		{
			Global = Locator.Current.GetService<Global>();
			Coin = coin;
		}
		
		public CoinViewModel Coin { get; }
		private CompositeDisposable Disposables { get; set; }
		private Global Global { get; }
	}
}