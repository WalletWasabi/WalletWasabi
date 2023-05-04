using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

public class PocketSelectionTests
{
	private LabelSelectionViewModel CreateLabelSelectionViewModel(Money amount, SmartLabel recipient)
	{
		var pw = "";
		var km = KeyManager.Recover(
			new Mnemonic("all all all all all all all all all all all all"),
			pw,
			Network.Main,
			KeyManager.GetAccountKeyPath(Network.Main, ScriptPubKeyType.Segwit));
		var address = BitcoinAddress.Create("bc1q7v7qfhwx55erxkc66nsv39x4azwufvy6zq8ya4", Network.Main);
		var info = new TransactionInfo(address, 100)
		{
			Amount = amount,
			FeeRate = new FeeRate(2m),
			Recipient = recipient
		};

		return new LabelSelectionViewModel(km, pw, info);
	}

	[Fact]
	public async Task WhiteListHighlightsAllLabelsInOtherPocketsThatContainTargetLabelAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out _, "Target");
		pockets.AddPocket(1.0M, out _, "David", "Adam", "Lucas");
		pockets.AddPocket(1.0M, out _, "Jumar");

		await selection.ResetAsync(pockets.ToArray());

		selection.GetLabel("Target").IsPointerOver = true;

		Assert.True(selection.GetLabel("Target").IsHighlighted);
		Assert.True(selection.GetLabel("Dan").IsHighlighted);
		Assert.True(selection.GetLabel("Roland").IsHighlighted);

		Assert.False(selection.GetLabel("David").IsHighlighted);
		Assert.False(selection.GetLabel("Adam").IsHighlighted);
		Assert.False(selection.GetLabel("Lucas").IsHighlighted);
		Assert.False(selection.GetLabel("Jumar").IsHighlighted);
	}

	[Fact]
	public async Task AllWhitelistPocketsAreOutputAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out var pocket2, "Target");
		pockets.AddPocket(1.0M, out var pocket3, "David", "Adam", "Lucas");
		pockets.AddPocket(1.0M, out var pocket4, "Jumar");

		await selection.ResetAsync(pockets.ToArray());

		var output = selection.GetUsedPockets();

		Assert.Contains(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.Contains(pocket3, output);
		Assert.Contains(pocket4, output);
	}

	[Fact]
	public async Task WhiteListHighlightsAllLabelsInOtherPocketsThatContainTargetLabelExceptThoseAvailableInOtherPocketsAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out _, "Dan");

		await selection.ResetAsync(pockets.ToArray());

		selection.GetLabel("Target").IsPointerOver = true;

		Assert.True(selection.GetLabel("Target").IsHighlighted);
		Assert.True(selection.GetLabel("Roland").IsHighlighted);
		Assert.False(selection.GetLabel("Dan").IsHighlighted);
	}

	[Fact]
	public async Task WhiteListHighlightsAllLabelsInOtherPocketsThatContainTargetLabelAndTheOtherLabelsInThosePocketsAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out _, "Dan");

		await selection.ResetAsync(pockets.ToArray());

		selection.GetLabel("Dan").IsPointerOver = true;

		Assert.True(selection.GetLabel("Target").IsHighlighted);
		Assert.True(selection.GetLabel("Roland").IsHighlighted);
		Assert.True(selection.GetLabel("Dan").IsHighlighted);
	}

	[Fact]
	public async Task WhiteListSwapsAllLabelsInOtherPocketsThatContainTargetLabelAndTheOtherLabelsInThosePocketsAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out _, "Dan");

		await selection.ResetAsync(pockets.ToArray());

		await selection.SwapLabelAsync(selection.GetLabel("Dan"));

		Assert.Contains(selection.GetLabel("Target"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Roland"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsBlackList);
	}

	[Fact]
	public async Task OutPutMatchesWhiteListScenario1Async()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out var pocket2, "Dan");

		await selection.ResetAsync(pockets.ToArray());

		await selection.SwapLabelAsync(selection.GetLabel("Dan"));

		var output = selection.GetUsedPockets();

		Assert.DoesNotContain(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
	}

	[Fact]
	public async Task BlackListHighlightsLabelsExclusivelyThatExistInMultiplePocketsAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out var pocket2, "Dan");

		await selection.ResetAsync(pockets.ToArray());

		await selection.SwapLabelAsync(selection.GetLabel("Dan"));

		Assert.Contains(selection.GetLabel("Target"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Roland"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsBlackList);

		selection.GetLabel("Dan").IsPointerOver = true;

		Assert.True(selection.GetLabel("Dan").IsHighlighted);
		Assert.False(selection.GetLabel("Roland").IsHighlighted);
		Assert.False(selection.GetLabel("Target").IsHighlighted);

		var output = selection.GetUsedPockets();

		Assert.DoesNotContain(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
	}

	[Fact]
	public async Task BlackListHighlightsDealWithMultipleOverlapsCorrectlyAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, "Target", "Dan");
		pockets.AddPocket(1.0M, out var pocket2, "Target");
		pockets.AddPocket(1.0M, out var pocket3, "Target", "Roland");

		await selection.ResetAsync(pockets.ToArray());

		await selection.SwapLabelAsync(selection.GetLabel("Target"));

		Assert.Contains(selection.GetLabel("Target"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Roland"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsBlackList);

		selection.GetLabel("Dan").IsPointerOver = true;

		Assert.True(selection.GetLabel("Dan").IsHighlighted);
		Assert.False(selection.GetLabel("Roland").IsHighlighted);
		Assert.True(selection.GetLabel("Target").IsHighlighted);

		var output = selection.GetUsedPockets();

		Assert.DoesNotContain(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
	}

	[Fact]
	public async Task WhiteListHighlightsDealWithMultipleOverlapsCorrectlyAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Target", "Dan");
		pockets.AddPocket(1.0M, out _, "Target");
		pockets.AddPocket(1.0M, out _, "Target", "Roland");

		await selection.ResetAsync(pockets.ToArray());

		selection.GetLabel("Dan").IsPointerOver = true;

		Assert.True(selection.GetLabel("Dan").IsHighlighted);
		Assert.False(selection.GetLabel("Roland").IsHighlighted);
		Assert.False(selection.GetLabel("Target").IsHighlighted);
	}

	[Fact]
	public async Task WhiteListSwapsGroupedLabelsInOtherPocketsThatContainTargetLabelExceptThoseAvailableInOtherPocketsAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, "Target", "Dan");
		pockets.AddPocket(1.0M, out var pocket2, "Target");
		pockets.AddPocket(1.0M, out var pocket3, "Target", "Roland");

		await selection.ResetAsync(pockets.ToArray());

		await selection.SwapLabelAsync(selection.GetLabel("Dan"));

		Assert.DoesNotContain(selection.GetLabel("Target"), selection.LabelsBlackList);
		Assert.DoesNotContain(selection.GetLabel("Roland"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsBlackList);

		var output = selection.GetUsedPockets();

		Assert.DoesNotContain(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.Contains(pocket3, output);
	}

	[Fact]
	public async Task MovesFromWhiteListHighlightsAllLabelsInOtherPocketsThatContainTargetLabelAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out _, "Target");
		pockets.AddPocket(1.0M, out _, "David", "Adam", "Lucas");
		pockets.AddPocket(1.0M, out _, "Jumar");

		await selection.ResetAsync(pockets.ToArray());

		await selection.SwapLabelAsync(selection.GetLabel("Target"));

		Assert.Contains(selection.GetLabel("Target"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Roland"), selection.LabelsBlackList);

		Assert.DoesNotContain(selection.GetLabel("David"), selection.LabelsBlackList);
		Assert.DoesNotContain(selection.GetLabel("Adam"), selection.LabelsBlackList);
		Assert.DoesNotContain(selection.GetLabel("Lucas"), selection.LabelsBlackList);
		Assert.DoesNotContain(selection.GetLabel("Jumar"), selection.LabelsBlackList);

		Assert.DoesNotContain(selection.GetLabel("Target"), selection.LabelsWhiteList);
		Assert.DoesNotContain(selection.GetLabel("Dan"), selection.LabelsWhiteList);
		Assert.DoesNotContain(selection.GetLabel("Roland"), selection.LabelsWhiteList);

		Assert.Contains(selection.GetLabel("David"), selection.LabelsWhiteList);
		Assert.Contains(selection.GetLabel("Adam"), selection.LabelsWhiteList);
		Assert.Contains(selection.GetLabel("Lucas"), selection.LabelsWhiteList);
		Assert.Contains(selection.GetLabel("Jumar"), selection.LabelsWhiteList);
	}

	[Fact]
	public async Task PrivateAndSemiPrivatePocketsAreHiddenAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(2.0M, out var pocket2, "Dan");
		pockets.AddPocket(1.0M, out var pocket3, CoinPocketHelper.SemiPrivateFundsText);

		await selection.ResetAsync(pockets.ToArray());

		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsWhiteList);

		// Not found.
		Assert.Throws<InvalidOperationException>(() => selection.GetLabel(CoinPocketHelper.PrivateFundsText));
		Assert.Throws<InvalidOperationException>(() => selection.GetLabel(CoinPocketHelper.SemiPrivateFundsText));
	}

	[Fact]
	public async Task UseOnlyPrivateFundsAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.1M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(2.0M, out var pocket2, "Dan");
		pockets.AddPocket(1.0M, out var pocket3, CoinPocketHelper.SemiPrivateFundsText);

		await selection.ResetAsync(pockets.ToArray());

		await selection.SwapLabelAsync(selection.GetLabel("Dan"));

		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsBlackList);
		Assert.True(selection.EnoughSelected);

		var output = selection.GetUsedPockets();
		Assert.DoesNotContain(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.Contains(pocket1, output);
	}

	[Fact]
	public async Task UsePrivateAndSemiPrivateFundsAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.5"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.9M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(2.0M, out var pocket2, "Dan");
		pockets.AddPocket(1.0M, out var pocket3, CoinPocketHelper.SemiPrivateFundsText);

		await selection.ResetAsync(pockets.ToArray());

		await selection.SwapLabelAsync(selection.GetLabel("Dan"));

		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsBlackList);
		Assert.True(selection.EnoughSelected);

		var output = selection.GetUsedPockets();
		Assert.Contains(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.Contains(pocket3, output);
	}

	[Fact]
	public async Task DoNotUsePrivateFundsAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), "Dan");

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(2.0M, out var pocket2, "Dan");
		pockets.AddPocket(1.0M, out var pocket3, CoinPocketHelper.SemiPrivateFundsText);

		await selection.ResetAsync(pockets.ToArray());

		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsWhiteList);
		Assert.True(selection.EnoughSelected);

		var output = selection.GetUsedPockets();
		Assert.DoesNotContain(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
	}

	[Fact]
	public async Task IncludePrivateFundsAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("2.5"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(2.0M, out var pocket2, "Dan");
		pockets.AddPocket(1.0M, out var pocket3, CoinPocketHelper.SemiPrivateFundsText);

		await selection.ResetAsync(pockets.ToArray());

		Assert.True(selection.EnoughSelected);

		var output = selection.GetUsedPockets();
		Assert.Contains(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
	}

	[Fact]
	public async Task IncludePrivateAndSemiPrivateFundsAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("2.5"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.6M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(1.0M, out var pocket2, "Dan");
		pockets.AddPocket(1.0M, out var pocket3, CoinPocketHelper.SemiPrivateFundsText);

		await selection.ResetAsync(pockets.ToArray());

		Assert.True(selection.EnoughSelected);

		var output = selection.GetUsedPockets();
		Assert.Contains(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.Contains(pocket3, output);
	}

	[Fact]
	public async Task StillIncludePrivateFundsAfterSwapAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);
		var pockets = new List<Pocket>();

		var privateCoin = LabelTestExtensions.CreateCoin(0.8m, "", 999);
		var privatePocket = new Pocket((CoinPocketHelper.PrivateFundsText, new CoinsView(new[] { privateCoin })));
		pockets.Add(privatePocket);

		pockets.AddPocket(0.3M, out var pocket2, "Dan");
		pockets.AddPocket(0.1M, out var pocket3, "Lucas");

		await selection.ResetAsync(pockets.ToArray());

		var usedCoins = new List<SmartCoin>
		{
			privateCoin
		};
		usedCoins.AddRange(pocket2.Coins);

		await selection.SetUsedLabelAsync(usedCoins, 10);
		var output = selection.GetUsedPockets();
		Assert.Contains(privatePocket, output);
		Assert.Contains(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.True(selection.EnoughSelected);

		await selection.SwapLabelAsync(selection.GetLabel("Lucas"));
		await selection.SwapLabelAsync(selection.GetLabel("Lucas"));
		Assert.True(selection.EnoughSelected);
	}

	[Fact]
	public async Task NotEnoughSelectedAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "David");
		pockets.AddPocket(2.0M, out _, "Dan");

		await selection.ResetAsync(pockets.ToArray());

		await selection.SwapLabelAsync(selection.GetLabel("David"));
		await selection.SwapLabelAsync(selection.GetLabel("Dan"));

		Assert.False(selection.EnoughSelected);
	}

	[Fact]
	public async Task NotEnoughSelectedWhenSameLabelFoundInSeveralPocketsAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), SmartLabel.Empty);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.4M, out _, "Dan");
		pockets.AddPocket(2.0M, out _, "Dan", "David");

		await selection.ResetAsync(pockets.ToArray());

		await selection.SwapLabelAsync(selection.GetLabel("Dan"));
		await selection.SwapLabelAsync(selection.GetLabel("Dan"));

		Assert.DoesNotContain(selection.GetLabel("Dan"), selection.LabelsBlackList);
		Assert.DoesNotContain(selection.GetLabel("David"), selection.LabelsWhiteList);

		Assert.False(selection.EnoughSelected);
	}

	[Fact]
	public async Task SetUsedLabelIgnoreCaseAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.0"), "Dan");

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.2M, out _, "Dan");
		pockets.AddPocket(1.2M, out _, "Lucas");

		await selection.ResetAsync(pockets.ToArray());

		var hdpk = LabelTestExtensions.NewKey("dan");
		var usedCoin = BitcoinFactory.CreateSmartCoin(hdpk, 1.0M);
		await selection.SetUsedLabelAsync(new[] { usedCoin }, privateThreshold: 10);

		Assert.Contains(selection.GetLabel("Lucas"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsWhiteList);
	}

	[Fact]
	public async Task SetUsedLabelIncludePrivateFundsAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.5"), "Dan");

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Dan");

		var privateCoins = new[]
		{
			BitcoinFactory.CreateSmartCoin(LabelTestExtensions.NewKey(anonymitySet: 999), 0.5m),
			BitcoinFactory.CreateSmartCoin(LabelTestExtensions.NewKey(anonymitySet: 999), 0.5m),
		};
		var coinsView = new CoinsView(privateCoins.ToArray());
		var pocket = new Pocket((SmartLabel.Empty, coinsView));
		pockets.Add(pocket);

		await selection.ResetAsync(pockets.ToArray());

		await selection.SetUsedLabelAsync(pockets.SelectMany(x => x.Coins), privateThreshold: 10);

		Assert.True(selection.EnoughSelected);
	}

	[Fact]
	public async Task SetUsedLabelIncludeSemiPrivateFundsAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("1.5"), "Dan");

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Dan");

		var semiPrivateCoins = new[]
		{
			BitcoinFactory.CreateSmartCoin(LabelTestExtensions.NewKey(anonymitySet: 5), 0.5m),
			BitcoinFactory.CreateSmartCoin(LabelTestExtensions.NewKey(anonymitySet: 5), 0.5m),
		};
		var coinsView = new CoinsView(semiPrivateCoins.ToArray());
		var pocket = new Pocket((SmartLabel.Empty, coinsView));
		pockets.Add(pocket);

		await selection.ResetAsync(pockets.ToArray());

		await selection.SetUsedLabelAsync(pockets.SelectMany(x => x.Coins), privateThreshold: 10);

		Assert.True(selection.EnoughSelected);
	}

	[Fact]
	public async Task SetUsedLabelIncludePrivateAndSemiPrivateFundsAsync()
	{
		var selection = CreateLabelSelectionViewModel(Money.Parse("2.5"), "Dan");

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Dan");

		var privateCoins = new[]
		{
			BitcoinFactory.CreateSmartCoin(LabelTestExtensions.NewKey(anonymitySet: 999), 0.5m),
			BitcoinFactory.CreateSmartCoin(LabelTestExtensions.NewKey(anonymitySet: 999), 0.5m),
		};
		var privateCoinsView = new CoinsView(privateCoins.ToArray());
		var privatePocket = new Pocket((SmartLabel.Empty, privateCoinsView));
		pockets.Add(privatePocket);

		var semiPrivateCoins = new[]
		{
			BitcoinFactory.CreateSmartCoin(LabelTestExtensions.NewKey(anonymitySet: 5), 0.5m),
			BitcoinFactory.CreateSmartCoin(LabelTestExtensions.NewKey(anonymitySet: 5), 0.5m),
		};
		var semiPrivateCoinsView = new CoinsView(semiPrivateCoins.ToArray());
		var semiPrivatePocket = new Pocket((SmartLabel.Empty, semiPrivateCoinsView));
		pockets.Add(semiPrivatePocket);

		await selection.ResetAsync(pockets.ToArray());

		await selection.SetUsedLabelAsync(pockets.SelectMany(x => x.Coins), privateThreshold: 10);

		Assert.True(selection.EnoughSelected);
	}

	[Fact]
	public async Task IsOtherSelectionPossibleCasesAsync()
	{
		var privatePocket = LabelTestExtensions.CreateSingleCoinPocket(1.0m, CoinPocketHelper.PrivateFundsText, anonSet: 999);
		var semiPrivatePocket = LabelTestExtensions.CreateSingleCoinPocket(1.0m, CoinPocketHelper.SemiPrivateFundsText, anonSet: 2);
		var danPocket = LabelTestExtensions.CreateSingleCoinPocket(1.0m, "Dan");
		var danLucasPocket = LabelTestExtensions.CreateSingleCoinPocket(1.0m, "Dan, Lucas");

		// Other pocket can be used case.
		var recipient = "Lucas";
		var selection = CreateLabelSelectionViewModel(Money.Parse("0.5"), recipient);
		await selection.ResetAsync(new[] { danPocket, danLucasPocket });
		Assert.True(selection.IsOtherSelectionPossible(danLucasPocket.Coins, recipient));

		// No other pocket can be used case.
		recipient = "Adam";
		selection = CreateLabelSelectionViewModel(Money.Parse("0.5"), recipient);
		var usedCoins = Pocket.Merge(danPocket, danLucasPocket).Coins;
		Assert.False(selection.IsOtherSelectionPossible(usedCoins, recipient));

		// Exact match. Recipient == pocket, no better selection.
		recipient = "Dan";
		selection = CreateLabelSelectionViewModel(Money.Parse("0.5"), recipient);
		Assert.False(selection.IsOtherSelectionPossible(danPocket.Coins, recipient));

		await selection.ResetAsync(new[] { privatePocket, danPocket, danLucasPocket });

		// Private funds are enough for the payment, no better selection.
		recipient = "Doesn't matter, it will use private coins";
		selection = CreateLabelSelectionViewModel(Money.Parse("0.5"), recipient);
		Assert.False(selection.IsOtherSelectionPossible(privatePocket.Coins, recipient));

		await selection.ResetAsync(new[] { semiPrivatePocket, danPocket, danLucasPocket });

		// Semi funds are enough for the payment, no better selection.
		Assert.False(selection.IsOtherSelectionPossible(semiPrivatePocket.Coins, recipient));

		selection = CreateLabelSelectionViewModel(Money.Parse("3.5"), recipient);
		await selection.ResetAsync(new[] { privatePocket, semiPrivatePocket, danPocket, danLucasPocket });

		// Private and semi funds are enough for the payment, no better selection.
		usedCoins = Pocket.Merge(privatePocket, semiPrivatePocket).Coins;
		Assert.False(selection.IsOtherSelectionPossible(usedCoins, recipient));
	}
}
