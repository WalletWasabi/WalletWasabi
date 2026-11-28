using System;
using System.Linq;
using System.Reflection;
using DynamicData;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Coins;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

public class CoinListViewModelTests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public void SingleCoinPocketPreservesSelectionAndPrivacyOptions(bool allowCoinjoiningCoinSelection, bool ignorePrivacyMode)
	{
		var pocket = LabelTestExtensions.CreateSingleCoinPocket(0.5m, "Pocket");
		var smartCoin = pocket.Coins.Single();
		smartCoin.CoinJoinInProgress = true;
		var services = DispatchProxy.Create<IServices, TipHeightServices>();
		var coin = new CoinModel(smartCoin, Network.Main, 50, services);
		using var coins = new SingleCoinList(pocket, coin);
		using var viewModel = new CoinListViewModel(null!, coins, [], allowCoinjoiningCoinSelection, ignorePrivacyMode);

		var item = Assert.Single(viewModel.CoinItems);

		Assert.Equal(ignorePrivacyMode, item.IgnorePrivacyMode);
		Assert.Equal(allowCoinjoiningCoinSelection, item.CanBeSelected);

		smartCoin.CoinJoinInProgress = false;

		Assert.True(item.CanBeSelected);
	}

	private sealed class SingleCoinList : ICoinListModel, IDisposable
	{
		private readonly SourceCache<CoinModel, int> _coins = new(x => x.Key);
		private readonly SourceCache<Pocket, LabelsArray> _pockets = new(x => x.Labels);

		public SingleCoinList(Pocket pocket, CoinModel coin)
		{
			_coins.AddOrUpdate(coin);
			_pockets.AddOrUpdate(pocket);
		}

		public IObservableCache<CoinModel, int> List => _coins;
		public IObservableCache<Pocket, LabelsArray> Pockets => _pockets;
		public CoinModel GetCoinModel(SmartCoin smartCoin) => _coins.Items.Single();

		public void Dispose()
		{
			_coins.Dispose();
			_pockets.Dispose();
		}
	}

	public class TipHeightServices : DispatchProxy
	{
		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			if (targetMethod?.Name == nameof(IServices.GetServerTipHeight))
			{
				return 200u;
			}

			throw new NotSupportedException();
		}
	}
}
