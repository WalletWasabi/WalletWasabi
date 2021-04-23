using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Alias;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public partial class WalletBalanceChartTileViewModel : TileViewModel
	{
		private readonly ReadOnlyObservableCollection<HistoryItemViewModel> _history;
		private readonly ReadOnlyObservableCollection<double> _yValues;
		private readonly ReadOnlyObservableCollection<double> _xValues;

		public WalletBalanceChartTileViewModel(ReadOnlyObservableCollection<HistoryItemViewModel> history)
		{
			_history = history;

			_history
				.ToObservableChangeSet()
				.Select(x => (double)x.Balance.ToDecimal(MoneyUnit.BTC))
				.Reverse()
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(out _yValues)
				.DisposeMany()
				.Subscribe();

			_history
				.ToObservableChangeSet()
				.Select(x => (double)x.Date.ToUnixTimeSeconds())
				.ObserveOn(RxApp.MainThreadScheduler)
				.Bind(out _xValues)
				.DisposeMany()
				.Subscribe();
		}

		public ReadOnlyObservableCollection<double> YValues => _yValues;

		public ReadOnlyObservableCollection<double> XValues => _xValues;
	}
}