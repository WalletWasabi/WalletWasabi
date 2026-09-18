using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Coinjoins;
using WalletWasabi.WabiSabi.Client;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

/// <summary>
/// The cost rows the single-coinjoin and the grouped-coinjoins details screens share.
/// </summary>
public class CoinjoinCostsViewModelTests
{
	[Fact]
	public void RecordedCostsAreBrokenDown()
	{
		var costs = new CoinjoinCosts(Money.Satoshis(700), Money.Satoshis(300), Money.Zero);
		var viewModel = new CoinjoinCostsViewModel(CreateAmount);

		viewModel.Update(CreateCoinjoin(costs, amount: Money.Satoshis(-1_000)));

		Assert.True(viewModel.IsBreakdownVisible);
		Assert.Equal(Money.Satoshis(700), viewModel.MiningFeeAmount?.Btc);
		Assert.Equal(Money.Satoshis(300), viewModel.WastedDustAmount?.Btc);

		// The "Fees" row is the sum of the rows beneath it.
		Assert.Equal(Money.Satoshis(1_000), viewModel.TotalFeeAmount?.Btc);

		Assert.False(viewModel.ArePaymentsVisible);
		Assert.Null(viewModel.PaymentsAmount);
	}

	[Fact]
	public void PaymentsAreShownSeparatelyAndAreNotCountedAsFees()
	{
		var costs = new CoinjoinCosts(Money.Satoshis(700), Money.Satoshis(300), Money.Coins(0.5m));
		var viewModel = new CoinjoinCostsViewModel(CreateAmount);

		viewModel.Update(CreateCoinjoin(costs, amount: -Money.Coins(0.5m) - Money.Satoshis(1_000)));

		Assert.True(viewModel.ArePaymentsVisible);
		Assert.Equal(Money.Coins(0.5m), viewModel.PaymentsAmount?.Btc);

		// The wallet sent half a bitcoin, but the coinjoin only cost it the fee.
		Assert.Equal(Money.Satoshis(1_000), viewModel.TotalFeeAmount?.Btc);
	}

	[Fact]
	public void CoinjoinsMadeBeforeTheCostsWereRecordedFallBackToASingleFigure()
	{
		var viewModel = new CoinjoinCostsViewModel(CreateAmount);

		viewModel.Update(CreateCoinjoin(costs: null, amount: Money.Satoshis(-1_234)));

		Assert.False(viewModel.IsBreakdownVisible);
		Assert.False(viewModel.ArePaymentsVisible);
		Assert.Equal(Money.Satoshis(1_234), viewModel.TotalFeeAmount?.Btc);
		Assert.Null(viewModel.MiningFeeAmount);
		Assert.Null(viewModel.WastedDustAmount);
	}

	[Fact]
	public void SelectingAnOlderCoinjoinAfterANewerOneClearsTheBreakdown()
	{
		// The same view model is reused as the transaction list updates, so nothing may linger.
		var viewModel = new CoinjoinCostsViewModel(CreateAmount);

		viewModel.Update(CreateCoinjoin(new CoinjoinCosts(Money.Satoshis(700), Money.Satoshis(300), Money.Coins(0.5m)), Money.Satoshis(-1_000)));
		viewModel.Update(CreateCoinjoin(costs: null, amount: Money.Satoshis(-1_234)));

		Assert.False(viewModel.IsBreakdownVisible);
		Assert.False(viewModel.ArePaymentsVisible);
		Assert.Null(viewModel.MiningFeeAmount);
		Assert.Null(viewModel.WastedDustAmount);
		Assert.Null(viewModel.PaymentsAmount);
	}

	private static Amount CreateAmount(Money? money) => new(money ?? Money.Zero);

	private static TransactionModel CreateCoinjoin(CoinjoinCosts? costs, Money amount) =>
		new()
		{
			OrderIndex = 0,
			Id = uint256.One,
			Labels = LabelsArray.Empty,
			Date = DateTimeOffset.UtcNow,
			DateString = "",
			DateToolTipString = "",
			Confirmations = 1,
			ConfirmedTooltip = "",
			Type = TransactionType.Coinjoin,
			Status = TransactionStatus.Confirmed,
			HexFunction = () => "",
			ForeignInputsFunction = () => [],
			WalletInputs = [],
			ForeignOutputsFunction = () => [],
			WalletOutputs = [],
			Amount = amount,
			CoinjoinCosts = costs
		};
}
