using Avalonia;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class PrivacyRingViewModel : RoutableViewModel
{
	private IObservable<Unit> _coinsUpdated;

	public PrivacyRingViewModel(WalletViewModel walletViewModel, IObservable<Unit> balanceChanged)
	{
		Title = "Wallet Coins";
		NextCommand = CancelCommand;

		_coinsUpdated =
			balanceChanged.ToSignal()
						  .Merge(walletViewModel
						  .WhenAnyValue(w => w.IsCoinJoining)
						  .ToSignal());

		_coinsUpdated
			.Select(_ => walletViewModel.Wallet.Coins)
			.Subscribe(RefreshCoinsList);
	}

	public override sealed string Title { get; protected set; }

	public ObservableCollectionExtended<PrivacyRingItemViewModel> Items { get; } = new();

	private void RefreshCoinsList(ICoinsView items)
	{
		var total = items.Sum(x => Math.Abs(x.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC)));

		var start = 0.0m;

		for (int i = 0; i < items.Count(); i++)
		{
			var coin = items.ElementAt(i);
			var end = start + (Math.Abs(coin.Amount.ToDecimal(NBitcoin.MoneyUnit.BTC)) / total);

			var item = new PrivacyRingItemViewModel(coin, (double)start, (double)end);

			Items.Add(item);

			start = end;
		}
	}

	public void Dispose()
	{
		foreach (var item in Items)
		{
			item.Dispose();
		}
	}
}

public class PrivacyRingItemViewModel : WalletCoinViewModel
{
	private const double TotalAngle = 2d * Math.PI;
	private const double UprightAngle = Math.PI / 2d;

	public PrivacyRingItemViewModel(SmartCoin coin, double start, double end) : base(coin)
	{
		var outerRadius = 200;
		var innerRadius = 150d;
		Arc1Size = new Size(outerRadius, outerRadius);
		Arc2Size = new Size(innerRadius, innerRadius);

		var margin = 2d;

		var startAngle = (TotalAngle * start) - UprightAngle;
		var endAngle = (TotalAngle * end) - UprightAngle;

		var outerOffset = outerRadius == 0 ? 0 : (margin / (TotalAngle * outerRadius) * TotalAngle);
		var innerOffset = innerRadius == 0 ? 0 : (margin / (TotalAngle * innerRadius) * TotalAngle);

		Origin1 = GetAnglePoint(outerRadius, startAngle + outerOffset);
		Arc1 = GetAnglePoint(outerRadius, endAngle - outerOffset);
		Origin2 = GetAnglePoint(innerRadius, endAngle - innerOffset);
		Arc2 = GetAnglePoint(innerRadius, startAngle + innerOffset);
	}

	public Point Origin1 { get; }
	public Point Arc1 { get; }
	public Size Arc1Size { get; }

	public Point Origin2 { get; }
	public Point Arc2 { get; }
	public Size Arc2Size { get; }

	private Point GetAnglePoint(double r, double angle)
	{
		var x = r * Math.Cos(angle);
		var y = r * Math.Sin(angle);
		return new Point(x, y);
	}
}
