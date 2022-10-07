using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

public class PocketSelectionTests
{
	public PocketSelectionTests()
	{
		Info = new TransactionInfo(100)
		{
			FeeRate = new FeeRate(2m)
		};
		Address = BitcoinAddress.Create("bc1q7v7qfhwx55erxkc66nsv39x4azwufvy6zq8ya4", Network.Main);
		Password = "";
		KeyManager = KeyManager.Recover(
			new Mnemonic("all all all all all all all all all all all all"),
			Password,
			Network.Main,
			KeyManager.GetAccountKeyPath(Network.Main));
	}

	private KeyManager KeyManager { get; }
	private TransactionInfo Info { get; }
	private string Password { get; }
	private BitcoinAddress Address { get; }
	private BitcoinStore? BitcoinStore { get; set; }

	private async Task<BitcoinStore> InitOrGetBitcoinStoreAsync()
	{
		if (BitcoinStore is null)
		{
			string dir = Common.GetWorkDir();

			await using IndexStore indexStore = new(Path.Combine(dir, "indexStore"), Network.Main, new SmartHeaderChain());
			await using AllTransactionStore transactionStore = new(Path.Combine(dir, "transactionStore"), Network.Main);
			MempoolService mempoolService = new();
			FileSystemBlockRepository blocks = new(Path.Combine(dir, "blocks"), Network.Main);

			// Construct BitcoinStore.
			BitcoinStore bitcoinStore = new(indexStore, transactionStore, mempoolService, blocks);
			await bitcoinStore.InitializeAsync();

			BitcoinStore = bitcoinStore;
		}

		return BitcoinStore;
	}

	[Fact]
	public async Task WhiteListHighlightsAllLabelsInOtherPocketsThatContainTargetLabelAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out _, "Target");
		pockets.AddPocket(1.0M, out _, "David", "Adam", "Lucas");
		pockets.AddPocket(1.0M, out _, "Jumar");

		selection.Reset(pockets.ToArray());

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
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out var pocket2, "Target");
		pockets.AddPocket(1.0M, out var pocket3, "David", "Adam", "Lucas");
		pockets.AddPocket(1.0M, out var pocket4, "Jumar");

		selection.Reset(pockets.ToArray());

		var output = selection.GetUsedPockets();

		Assert.Contains(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.Contains(pocket3, output);
		Assert.Contains(pocket4, output);
	}

	[Fact]
	public async Task WhiteListHighlightsAllLabelsInOtherPocketsThatContainTargetLabelExceptThoseAvailableInOtherPocketsAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out _, "Dan");

		selection.Reset(pockets.ToArray());

		selection.GetLabel("Target").IsPointerOver = true;

		Assert.True(selection.GetLabel("Target").IsHighlighted);
		Assert.True(selection.GetLabel("Roland").IsHighlighted);
		Assert.False(selection.GetLabel("Dan").IsHighlighted);
	}

	[Fact]
	public async Task WhiteListHighlightsAllLabelsInOtherPocketsThatContainTargetLabelAndTheOtherLabelsInThosePocketsAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out _, "Dan");

		selection.Reset(pockets.ToArray());

		selection.GetLabel("Dan").IsPointerOver = true;

		Assert.True(selection.GetLabel("Target").IsHighlighted);
		Assert.True(selection.GetLabel("Roland").IsHighlighted);
		Assert.True(selection.GetLabel("Dan").IsHighlighted);
	}

	[Fact]
	public async Task WhiteListSwapsAllLabelsInOtherPocketsThatContainTargetLabelAndTheOtherLabelsInThosePocketsAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out _, "Dan");

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("Dan"));

		Assert.Contains(selection.GetLabel("Target"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Roland"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsBlackList);
	}

	[Fact]
	public async Task OutPutMatchesWhiteListScenario1Async()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out var pocket2, "Dan");

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("Dan"));

		var output = selection.GetUsedPockets();

		Assert.DoesNotContain(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
	}

	[Fact]
	public async Task BlackListHighlightsLabelsExclusivelyThatExistInMultiplePocketsAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out var pocket2, "Dan");

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("Dan"));

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
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, "Target", "Dan");
		pockets.AddPocket(1.0M, out var pocket2, "Target");
		pockets.AddPocket(1.0M, out var pocket3, "Target", "Roland");

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("Target"));

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
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Target", "Dan");
		pockets.AddPocket(1.0M, out _, "Target");
		pockets.AddPocket(1.0M, out _, "Target", "Roland");

		selection.Reset(pockets.ToArray());

		selection.GetLabel("Dan").IsPointerOver = true;

		Assert.True(selection.GetLabel("Dan").IsHighlighted);
		Assert.False(selection.GetLabel("Roland").IsHighlighted);
		Assert.False(selection.GetLabel("Target").IsHighlighted);
	}

	[Fact]
	public async Task WhiteListSwapsGroupedLabelsInOtherPocketsThatContainTargetLabelExceptThoseAvailableInOtherPocketsAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, "Target", "Dan");
		pockets.AddPocket(1.0M, out var pocket2, "Target");
		pockets.AddPocket(1.0M, out var pocket3, "Target", "Roland");

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("Dan"));

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
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, out _, "Target");
		pockets.AddPocket(1.0M, out _, "David", "Adam", "Lucas");
		pockets.AddPocket(1.0M, out _, "Jumar");

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("Target"));

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
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(2.0M, out var pocket2, "Dan");
		pockets.AddPocket(1.0M, out var pocket3, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsWhiteList);

		// Not found.
		Assert.Throws<InvalidOperationException>(() => selection.GetLabel(CoinPocketHelper.PrivateFundsText));
		Assert.Throws<InvalidOperationException>(() => selection.GetLabel(CoinPocketHelper.SemiPrivateFundsText));
	}

	[Fact]
	public async Task UseOnlyPrivateFundsAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.1M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(2.0M, out var pocket2, "Dan");
		pockets.AddPocket(1.0M, out var pocket3, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("Dan"));

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
		Info.Amount = Money.Parse("1.5");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.9M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(2.0M, out var pocket2, "Dan");
		pockets.AddPocket(1.0M, out var pocket3, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("Dan"));

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
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(2.0M, out var pocket2, "Dan");
		pockets.AddPocket(1.0M, out var pocket3, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

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
		Info.Amount = Money.Parse("2.5");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(2.0M, out var pocket2, "Dan");
		pockets.AddPocket(1.0M, out var pocket3, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		Assert.True(selection.EnoughSelected);

		var output = selection.GetUsedPockets();
		Assert.Contains(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
	}

	[Fact]
	public async Task IncludePrivateAndSemiPrivateFundsAsync()
	{
		Info.Amount = Money.Parse("2.5");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.6M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(1.0M, out var pocket2, "Dan");
		pockets.AddPocket(1.0M, out var pocket3, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		Assert.True(selection.EnoughSelected);

		var output = selection.GetUsedPockets();
		Assert.Contains(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.Contains(pocket3, output);
	}

	[Fact]
	public async Task StillIncludePrivateFundsAfterSwapAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);
		var pockets = new List<Pocket>();

		var privateCoin = LabelTestExtensions.CreateCoin(0.8m, "", 999);
		var privatePocket = new Pocket((CoinPocketHelper.PrivateFundsText, new CoinsView(new[] { privateCoin })));
		pockets.Add(privatePocket);

		pockets.AddPocket(0.3M, out var pocket2, "Dan");
		pockets.AddPocket(0.1M, out var pocket3, "Lucas");

		selection.Reset(pockets.ToArray());

		var usedCoins = new List<SmartCoin>
		{
			privateCoin
		};
		usedCoins.AddRange(pocket2.Coins);

		selection.SetUsedLabel(usedCoins, 10);
		var output = selection.GetUsedPockets();
		Assert.Contains(privatePocket, output);
		Assert.Contains(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.True(selection.EnoughSelected);

		selection.SwapLabel(selection.GetLabel("Lucas"));
		selection.SwapLabel(selection.GetLabel("Lucas"));
		Assert.True(selection.EnoughSelected);
	}

	[Fact]
	public async Task NotEnoughSelectedAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "David");
		pockets.AddPocket(2.0M, out _, "Dan");

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("David"));
		selection.SwapLabel(selection.GetLabel("Dan"));

		Assert.False(selection.EnoughSelected);
	}

	[Fact]
	public async Task AutoSelectOnlyPrivatePocketAsync()
	{
		Info.Amount = Money.Parse("0.7");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.8M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(0.6M, out var pocket2, "Dan");
		pockets.AddPocket(0.7M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(0.7M, out var pocket4, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");
		Assert.Contains(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.DoesNotContain(pocket4, output);
	}

	[Fact]
	public async Task AutoSelectPrivateAndSemiPrivatePocketsAsync()
	{
		Info.Amount = Money.Parse("0.7");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.4M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(0.6M, out var pocket2, "Dan");
		pockets.AddPocket(0.7M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(0.4M, out var pocket4, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");
		Assert.Contains(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.Contains(pocket4, output);
	}

	[Fact]
	public async Task AutoSelectPrivateAndSemiPrivateAndKnownPocketsAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.3M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(0.5M, out var pocket2, "Dan");
		pockets.AddPocket(0.4M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(0.3M, out var pocket4, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");
		Assert.Contains(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.Contains(pocket4, output);
	}

	[Fact]
	public async Task AutoSelectPrivateAndSemiPrivateAndUnknownPocketsAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.3M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(0.1M, out var pocket2, "Dan");
		pockets.AddPocket(0.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(0.4M, out var pocket4, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");
		Assert.Contains(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.Contains(pocket3, output);
		Assert.Contains(pocket4, output);
	}

	[Fact]
	public async Task AutoSelectAllPocketsAsync()
	{
		Info.Amount = Money.Parse("2.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.8M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(0.3M, out var pocket2, "Dan");
		pockets.AddPocket(0.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(0.5M, out var pocket4, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");
		Assert.Contains(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.Contains(pocket3, output);
		Assert.Contains(pocket4, output);
	}

	[Fact]
	public async Task AutoSelectOnlyKnownByRecipientPocketsAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(1.1M, out var pocket2, "Dan");
		pockets.AddPocket(0.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(1.1M, out var pocket4, "David", "Lucas");
		pockets.AddPocket(1.1M, out var pocket5, "David");
		pockets.AddPocket(1.1M, out var pocket6, "Lucas");
		pockets.AddPocket(1.1M, out var pocket7, "David", "Lucas", "Dan");
		pockets.AddPocket(1.0M, out var pocket8, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets(new SmartLabel("David", "Lucas"));
		Assert.DoesNotContain(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.Contains(pocket4, output);
		Assert.DoesNotContain(pocket5, output);
		Assert.DoesNotContain(pocket6, output);
		Assert.DoesNotContain(pocket7, output);
		Assert.DoesNotContain(pocket8, output);
	}

	[Fact]
	public async Task AutoSelectOnlyKnownByRecipientPocketsCaseInsensitiveAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(2.8M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(1.1M, out var pocket2, "Dan");
		pockets.AddPocket(1.1M, out var pocket3, "Lucas", "Dan");
		pockets.AddPocket(1.1M, out var pocket4, "dan");

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets(new SmartLabel("dAN"));
		Assert.DoesNotContain(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.Contains(pocket4, output);
	}

	[Fact]
	public async Task AutoSelectKnownByRecipientPocketsAsync()
	{
		Info.Amount = Money.Parse("1.9");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.8M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(1.1M, out var pocket2, "Dan");
		pockets.AddPocket(1.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(1.1M, out var pocket4, "David", "Lucas");
		pockets.AddPocket(0.1M, out var pocket5, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("David");
		Assert.Contains(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.Contains(pocket4, output);
		Assert.Contains(pocket5, output);
	}

	[Fact]
	public async Task AutoSelectKnownByMultipleRecipientPocketsAsync()
	{
		Info.Amount = Money.Parse("1.9");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.8M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(1.1M, out var pocket2, "Dan");
		pockets.AddPocket(0.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(1.1M, out var pocket4, "David", "Lucas", "Dan");
		pockets.AddPocket(1.1M, out var pocket5, "David");
		pockets.AddPocket(1.1M, out var pocket6, "Lucas");
		pockets.AddPocket(1.1M, out var pocket7, "David", "Lucas", "Dan", "Roland");
		pockets.AddPocket(1.1M, out var pocket8, "David", "Dan");
		pockets.AddPocket(0.1M, out var pocket9, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("David, Lucas");
		Assert.Contains(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.Contains(pocket4, output);
		Assert.DoesNotContain(pocket5, output);
		Assert.DoesNotContain(pocket6, output);
		Assert.DoesNotContain(pocket7, output);
		Assert.DoesNotContain(pocket8, output);
		Assert.Contains(pocket9, output);
	}

	[Fact]
	public async Task AutoSelectMultipleKnownByMultipleRecipientPocketsAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.2M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(1.1M, out var pocket2, "Dan");
		pockets.AddPocket(0.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(0.4M, out var pocket4, "David", "Lucas", "Dan");
		pockets.AddPocket(0.6M, out var pocket5, "David");
		pockets.AddPocket(0.5M, out var pocket6, "Lucas");
		pockets.AddPocket(0.5M, out var pocket7, "David", "Lucas", "Dan", "Roland");
		pockets.AddPocket(0.5M, out var pocket8, "David", "Dan");
		pockets.AddPocket(0.1M, out var pocket9, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("David, Lucas");
		Assert.Contains(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.Contains(pocket4, output);
		Assert.Contains(pocket5, output);
		Assert.DoesNotContain(pocket6, output);
		Assert.DoesNotContain(pocket7, output);
		Assert.DoesNotContain(pocket8, output);
		Assert.Contains(pocket9, output);
	}

	[Fact]
	public async Task AutoSelectRequiredKnownByRecipientPocketsAsync()
	{
		Info.Amount = Money.Parse("1.3");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.2M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(1.1M, out var pocket2, "Dan");
		pockets.AddPocket(1.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(0.5M, out var pocket4, "David");
		pockets.AddPocket(0.4M, out var pocket5, "David", "Max");
		pockets.AddPocket(0.6M, out var pocket6, "David", "Lucas", "Dan");
		pockets.AddPocket(0.1M, out var pocket7, CoinPocketHelper.SemiPrivateFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("David");
		Assert.Contains(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.Contains(pocket4, output);
		Assert.DoesNotContain(pocket5, output);
		Assert.Contains(pocket6, output);
		Assert.Contains(pocket7, output);
	}

	[Fact]
	public async Task NotEnoughSelectedWhenSameLabelFoundInSeveralPocketsAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.4M, out _, "Dan");
		pockets.AddPocket(2.0M, out _, "Dan", "David");

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("Dan"));
		selection.SwapLabel(selection.GetLabel("Dan"));

		Assert.DoesNotContain(selection.GetLabel("Dan"), selection.LabelsBlackList);
		Assert.DoesNotContain(selection.GetLabel("David"), selection.LabelsWhiteList);

		Assert.False(selection.EnoughSelected);
	}

	[Fact]
	public async Task SetUsedLabelIgnoreCaseAsync()
	{
		Info.Amount = Money.Parse("1.0");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.2M, out var pocket1, "Dan");
		pockets.AddPocket(1.2M, out var pocket2, "Lucas");

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");
		Assert.Contains(pocket1, output);
		Assert.DoesNotContain(pocket2, output);

		var hdpk = LabelTestExtensions.NewKey("dan");
		var usedCoin = BitcoinFactory.CreateSmartCoin(hdpk, 1.0M);
		selection.SetUsedLabel(new[] { usedCoin }, privateThreshold: 10);

		Assert.Contains(selection.GetLabel("Lucas"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsWhiteList);
	}

	[Fact]
	public async Task SetUsedLabelIncludePrivateFundsAsync()
	{
		Info.Amount = Money.Parse("1.5");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

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

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");

		selection.SetUsedLabel(output.SelectMany(x => x.Coins), privateThreshold: 10);

		Assert.True(selection.EnoughSelected);
	}

	[Fact]
	public async Task SetUsedLabelIncludeSemiPrivateFundsAsync()
	{
		Info.Amount = Money.Parse("1.5");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

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

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");

		selection.SetUsedLabel(output.SelectMany(x => x.Coins), privateThreshold: 10);

		Assert.True(selection.EnoughSelected);
	}

	[Fact]
	public async Task SetUsedLabelIncludePrivateAndSemiPrivateFundsAsync()
	{
		Info.Amount = Money.Parse("2.5");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);

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

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");

		selection.SetUsedLabel(output.SelectMany(x => x.Coins), privateThreshold: 10);

		Assert.True(selection.EnoughSelected);
	}

	[Fact]
	public async Task IsOtherSelectionPossibleCasesAsync()
	{
		Info.Amount = Money.Parse("0.5");

		var bitcoinStore = await InitOrGetBitcoinStoreAsync();
		var selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);
		var pockets = new List<Pocket>();

		var privatePocket = LabelTestExtensions.CreateSingleCoinPocket(1.0m, CoinPocketHelper.PrivateFundsText, anonSet: 999);
		var semiPrivatePocket = LabelTestExtensions.CreateSingleCoinPocket(1.0m, CoinPocketHelper.SemiPrivateFundsText, anonSet: 2);

		pockets.Add(LabelTestExtensions.CreateSingleCoinPocket(1.0m, "Dan"));
		pockets.Add(LabelTestExtensions.CreateSingleCoinPocket(1.0m, "Dan, Lucas"));
		selection.Reset(pockets.ToArray());

		// Other pocket can be used case.
		var recipient = "Lucas";
		var output = selection.AutoSelectPockets(recipient);
		Assert.True(selection.IsOtherSelectionPossible(output.SelectMany(x => x.Coins), recipient));

		// No other pocket can be used case.
		recipient = "Adam";
		output = selection.AutoSelectPockets(recipient);
		Assert.False(selection.IsOtherSelectionPossible(output.SelectMany(x => x.Coins), recipient));

		// Exact match. Recipient == pocket, no better selection.
		recipient = "Dan";
		output = selection.AutoSelectPockets(recipient);
		Assert.False(selection.IsOtherSelectionPossible(output.SelectMany(x => x.Coins), recipient));

		pockets.Add(privatePocket);
		selection.Reset(pockets.ToArray());

		// Private funds are enough for the payment, no better selection.
		recipient = "Doesn't matter, it will use private coins";
		output = selection.AutoSelectPockets(recipient);
		Assert.False(selection.IsOtherSelectionPossible(output.SelectMany(x => x.Coins), recipient));

		pockets.Remove(privatePocket);
		pockets.Add(semiPrivatePocket);
		Info.Amount = Money.Parse("0.5");
		selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);
		selection.Reset(pockets.ToArray());

		// Semi funds are enough for the payment, no better selection.
		recipient = "Doesn't matter, it will use semi private coins";
		output = selection.AutoSelectPockets(recipient);
		Assert.False(selection.IsOtherSelectionPossible(output.SelectMany(x => x.Coins), recipient));

		pockets.Add(privatePocket);
		Info.Amount = Money.Parse("1.5");
		selection = new LabelSelectionViewModel(KeyManager, Password, bitcoinStore, Info, Address);
		selection.Reset(pockets.ToArray());

		// Private and semi funds are enough for the payment, no better selection.
		recipient = "Doesn't matter, it will use semi private coins";
		output = selection.AutoSelectPockets(recipient);
		Assert.False(selection.IsOtherSelectionPossible(output.SelectMany(x => x.Coins), recipient));
	}
}
