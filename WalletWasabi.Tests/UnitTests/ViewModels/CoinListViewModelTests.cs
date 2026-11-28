using System;
using System.Collections.Generic;
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
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class CoinListViewModelTests
{
	[Fact]
	public void ReplacingCoinsInSamePocketUpdatesDisplayedCoins()
	{
		var transaction = BitcoinFactory.CreateSmartTransaction(ownOutputCount: 3);
		var coins = transaction.WalletOutputs.ToArray();
		using var model = new TestCoinListModel();
		model.SetCoins(coins[0], coins[1]);

		// No navigation, clipboard operation or rendered control is used.
		using var viewModel = new CoinListViewModel(
			null!, model, new List<CoinModel>(),
			allowCoinjoiningCoinSelection: true,
			ignorePrivacyMode: false,
			allowSelection: false);

		Assert.Equal(2, viewModel.CoinItems.Count);
		Assert.Contains(viewModel.CoinItems, x => x.Coin.IsSame(model.GetCoinModel(coins[0])));

		model.SetCoins(coins[1], coins[2]);

		var displayed = viewModel.CoinItems
			.Select(x => x.Coin.GetSmartCoin().Outpoint)
			.ToHashSet();

		Assert.Equal(2, displayed.Count);
		Assert.DoesNotContain(coins[0].Outpoint, displayed);
		Assert.Contains(coins[1].Outpoint, displayed);
		Assert.Contains(coins[2].Outpoint, displayed);
	}

	[Fact]
	public void UpdatingPocketPreservesSelectionOfRemainingCoin()
	{
		var transaction = BitcoinFactory.CreateSmartTransaction(ownOutputCount: 3);
		var coins = transaction.WalletOutputs.ToArray();
		using var model = new TestCoinListModel();
		model.SetCoins(coins[0], coins[1]);

		using var viewModel = new CoinListViewModel(
			null!, model, new List<CoinModel>(),
			allowCoinjoiningCoinSelection: true,
			ignorePrivacyMode: false,
			allowSelection: true);

		viewModel.CoinItems.Single(
			x => x.Coin.GetSmartCoin().Outpoint == coins[1].Outpoint).IsSelected = true;
		Assert.Contains(viewModel.Selection,
			x => x.GetSmartCoin().Outpoint == coins[1].Outpoint);

		model.SetCoins(coins[1], coins[2]);

		Assert.Contains(viewModel.Selection,
			x => x.GetSmartCoin().Outpoint == coins[1].Outpoint);
		Assert.DoesNotContain(viewModel.Selection,
			x => x.GetSmartCoin().Outpoint == coins[2].Outpoint);
	}

	[Fact]
	public void UpdatingPocketPreservesExpandedGroups()
	{
		var transaction = BitcoinFactory.CreateSmartTransaction(ownOutputCount: 5);
		var coins = transaction.WalletOutputs.ToArray();
		using var model = new TestCoinListModel();
		model.SetPockets(
			("first", new[] { coins[0], coins[1] }),
			("second", new[] { coins[3], coins[4] }));

		using var viewModel = new CoinListViewModel(
			null!, model, new List<CoinModel>(),
			allowCoinjoiningCoinSelection: true,
			ignorePrivacyMode: false,
			allowSelection: false);

		var original = viewModel.TreeDataGridSource.Items
			.OfType<PocketViewModel>()
			.Single(x => x.Labels.ToString() == "first");
		original.IsExpanded = true;

		model.SetPockets(
			("first", new[] { coins[1], coins[2] }),
			("second", new[] { coins[3], coins[4] }));

		var pockets = viewModel.TreeDataGridSource.Items
			.OfType<PocketViewModel>().ToArray();
		var updated = pockets.Single(x => x.Labels.ToString() == "first");

		Assert.NotSame(original, updated);
		Assert.True(updated.IsExpanded);
		Assert.False(pockets.Single(x => x.Labels.ToString() == "second").IsExpanded);
		Assert.Contains(updated.Children,
			x => x.Coin.GetSmartCoin().Outpoint == coins[2].Outpoint);
	}

	private sealed class TestCoinListModel : ICoinListModel, IDisposable
	{
		private readonly SourceCache<CoinModel, int> _coins = new(x => x.Key);
		private readonly SourceCache<Pocket, LabelsArray> _pockets = new(x => x.Labels);
		private readonly IServices _services = DispatchProxy.Create<IServices, TipServicesProxy>();

		public IObservableCache<CoinModel, int> List => _coins;
		public IObservableCache<Pocket, LabelsArray> Pockets => _pockets;

		public CoinModel GetCoinModel(SmartCoin coin) =>
			_coins.Items.Single(x => x.GetSmartCoin().Outpoint == coin.Outpoint);

		public void SetCoins(params SmartCoin[] coins) =>
			SetPockets(("test", coins));

		public void SetPockets(params (string Label, SmartCoin[] Coins)[] pockets)
		{
			_coins.Edit(updater =>
			{
				updater.Clear();
				updater.AddOrUpdate(pockets.SelectMany(x => x.Coins).Select(
					coin => new CoinModel(coin, Network.Main, 50, _services)));
			});

			_pockets.Edit(updater =>
			{
				updater.AddOrUpdate(pockets.Select(
					x => new Pocket((new LabelsArray(x.Label), new CoinsView(x.Coins)))));
			});
		}

		public void Dispose()
		{
			_pockets.Dispose();
			_coins.Dispose();
		}
	}

	public class TipServicesProxy : DispatchProxy
	{
		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			if (targetMethod?.Name == nameof(IServices.GetServerTipHeight))
			{
				return 200u;
			}

			throw new NotSupportedException(targetMethod?.Name);
		}
	}
}
