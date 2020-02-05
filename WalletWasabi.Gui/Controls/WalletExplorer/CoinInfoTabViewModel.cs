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
		public CoinInfoTabViewModel(string title, CoinViewModel targetVM) : base(title)
		{
			Global = Locator.Current.GetService<Global>();
			TargetVM = targetVM;
		}
		
		public object TargetVM { get; }
		private CompositeDisposable Disposables { get; set; }
		private Global Global { get; }

		public override void OnOpen()
		{
			Disposables = Disposables is null ?
				new CompositeDisposable() :
				throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");
		}

		public override bool OnClose()
		{
			Disposables?.Dispose();
			Disposables = null;
			return base.OnClose();
		}
	}
}