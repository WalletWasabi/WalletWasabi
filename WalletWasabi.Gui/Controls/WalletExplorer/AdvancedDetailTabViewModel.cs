using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using Splat;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class AdvancedDetailTabViewModel : ViewModelBase
	{
		private CompositeDisposable Disposables { get; set; }
		private Global Global { get; }
		private ViewModelBase TargetVM { get; }

		public AdvancedDetailTabViewModel(ViewModelBase targetVM)
		{
			Global = Locator.Current.GetService<Global>();
			TargetVM = targetVM;
		}

		private void OnOpen()
		{
			Disposables = Disposables is null ?
				new CompositeDisposable() :
				throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");
		}

		public void OnClose()
		{
			// Do not dispose the RootList here. It will be reused next time when you open CoinJoinTab or SendTab.
			Disposables?.Dispose();
			Disposables = null;
		}
	}
}