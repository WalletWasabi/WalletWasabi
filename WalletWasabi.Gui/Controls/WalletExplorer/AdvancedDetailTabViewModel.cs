using System;
using System.Reactive.Disposables;
using Splat;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class AdvancedDetailTabViewModel<TViewModel> : WasabiDocumentTabViewModel
									   where TViewModel : ViewModelBase
	{
		private CompositeDisposable Disposables { get; set; }
		private Global Global { get; }
		private TViewModel TargetVM { get; }



		public AdvancedDetailTabViewModel(string Title, TViewModel targetVM) : base(Title)
		{
			Global = Locator.Current.GetService<Global>();
			TargetVM = targetVM;
		}

		public override void OnOpen()
		{
			Disposables = Disposables is null ?
				new CompositeDisposable() :
				throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");
		}

		public override bool OnClose()
		{
			// Do not dispose the RootList here. It will be reused next time when you open CoinJoinTab or SendTab.
			Disposables?.Dispose();
			Disposables = null;
			return true;
		}
	}
}