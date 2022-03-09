using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

internal class TestCoinsView : ICoinsView
{
	private Money _total;

	public TestCoinsView(Money total)
	{
		_total = total;
	}

	public IEnumerator<SmartCoin> GetEnumerator()
	{
		throw new NotImplementedException();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public ICoinsView AtBlockHeight(Height height)
	{
		throw new NotImplementedException();
	}

	public ICoinsView Available()
	{
		throw new NotImplementedException();
	}

	public ICoinsView ChildrenOf(SmartCoin coin)
	{
		throw new NotImplementedException();
	}

	public ICoinsView CoinJoinInProcess()
	{
		throw new NotImplementedException();
	}

	public ICoinsView Confirmed()
	{
		throw new NotImplementedException();
	}

	public ICoinsView DescendantOf(SmartCoin coin)
	{
		throw new NotImplementedException();
	}

	public ICoinsView DescendantOfAndSelf(SmartCoin coin)
	{
		throw new NotImplementedException();
	}

	public ICoinsView FilterBy(Func<SmartCoin, bool> expression)
	{
		throw new NotImplementedException();
	}

	public ICoinsView OutPoints(ISet<OutPoint> outPoints)
	{
		throw new NotImplementedException();
	}

	public ICoinsView CreatedBy(uint256 txid)
	{
		throw new NotImplementedException();
	}

	public ICoinsView SpentBy(uint256 txid)
	{
		throw new NotImplementedException();
	}

	public SmartCoin[] ToArray()
	{
		throw new NotImplementedException();
	}

	public Money TotalAmount() => _total;

	public ICoinsView Unconfirmed()
	{
		throw new NotImplementedException();
	}

	public ICoinsView Unspent()
	{
		throw new NotImplementedException();
	}

	public bool TryGetByOutPoint(OutPoint outpoint, out SmartCoin? coin)
	{
		throw new NotImplementedException();
	}
}

public class PocketSelectionTests
{
	[Fact]
	public void WhiteListHighlightsAllLabelsInOtherPocketsThatContainTargetLabel()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

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
	public void AllWhitelistPocketsAreOutput()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

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
	public void WhiteListHighlightsAllLabelsInOtherPocketsThatContainTargetLabelExceptThoseAvailableInOtherPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

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
	public void WhiteListHighlightsAllLabelsInOtherPocketsThatContainTargetLabelAndTheOtherLabelsInThosePockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

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
	public void WhiteListSwapsAllLabelsInOtherPocketsThatContainTargetLabelAndTheOtherLabelsInThosePockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

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
	public void OutPutMatchesWhiteListScenario1()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

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
	public void BlackListHighlightsLabelsExclusivelyThatExistInMultiplePockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

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
	public void BlackListHighlightsDealWithMultipleOverlapsCorrectly()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

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
	public void WhiteListHighlightsDealWithMultipleOverlapsCorrectly()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

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
	public void WhiteListSwapsGroupedLabelsInOtherPocketsThatContainTargetLabelExceptThoseAvailableInOtherPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

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
	public void MovesFromWhiteListHighlightsAllLabelsInOtherPocketsThatContainTargetLabel()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

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
	public void PrivatePocketIsHidden()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(2.0M, out var pocket2, "Dan");

		selection.Reset(pockets.ToArray());

		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsWhiteList);

		// Not found.
		Assert.Throws<InvalidOperationException>(() => selection.GetLabel(CoinPocketHelper.PrivateFundsText));
	}

	[Fact]
	public void UseOnlyPrivateFunds()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(2.0M, out var pocket2, "Dan");

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("Dan"));

		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsBlackList);
		Assert.True(selection.EnoughSelected);

		var output = selection.GetUsedPockets();
		Assert.DoesNotContain(pocket2, output);
		Assert.Contains(pocket1, output);
	}

	[Fact]
	public void DoNotUsePrivateFunds()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(2.0M, out var pocket2, "Dan");

		selection.Reset(pockets.ToArray());

		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsWhiteList);
		Assert.True(selection.EnoughSelected);

		var output = selection.GetUsedPockets();
		Assert.DoesNotContain(pocket1, output);
		Assert.Contains(pocket2, output);
	}

	[Fact]
	public void IncludePrivateFunds()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("2.5"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(2.0M, out var pocket2, "Dan");

		selection.Reset(pockets.ToArray());

		Assert.True(selection.EnoughSelected);

		var output = selection.GetUsedPockets();
		Assert.Contains(pocket1, output);
		Assert.Contains(pocket2, output);
	}

	[Fact]
	public void NotEnoughSelected()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out _, "David");
		pockets.AddPocket(2.0M, out _, "Dan");

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("David"));
		selection.SwapLabel(selection.GetLabel("Dan"));

		Assert.False(selection.EnoughSelected);
	}

	[Fact]
	public void AutoSelectOnlyPrivatePocket()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("0.7"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.7M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(0.6M, out var pocket2, "Dan");
		pockets.AddPocket(0.7M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");
		Assert.Contains(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
	}

	[Fact]
	public void AutoSelectOnlyKnownPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.8M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(1.1M, out var pocket2, "Dan");
		pockets.AddPocket(0.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(0.1M, out var pocket4, "David", "Lucas");
		pockets.AddPocket(0.15M, out var pocket5, "David");
		pockets.AddPocket(0.2M, out var pocket6, "Lucas");
		pockets.AddPocket(0.3M, out var pocket7, "David", "Lucas", "Dan");

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets(new SmartLabel("David", "Lucas"));
		Assert.DoesNotContain(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.Contains(pocket4, output);
		Assert.Contains(pocket5, output);
		Assert.Contains(pocket6, output);
		Assert.Contains(pocket7, output);
	}

	[Fact]
	public void AutoSelectOnlyUnknownPocket()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("0.7"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.5M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(0.5M, out var pocket2, "Dan");
		pockets.AddPocket(0.8M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");
		Assert.DoesNotContain(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.Contains(pocket3, output);
	}

	[Fact]
	public void AutoSelectKnownAndUnknownPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.5M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(0.5M, out var pocket2, "Dan");
		pockets.AddPocket(0.8M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");
		Assert.DoesNotContain(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.Contains(pocket3, output);
	}

	[Fact]
	public void AutoSelectPrivateAndKnownPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.8M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(0.5M, out var pocket2, "Dan");
		pockets.AddPocket(0.4M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");
		Assert.Contains(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
	}

	[Fact]
	public void AutoSelectPrivateAndUnknownPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.8M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(0.1M, out var pocket2, "Dan");
		pockets.AddPocket(0.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");
		Assert.Contains(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.Contains(pocket3, output);
	}

	[Fact]
	public void AutoSelectAllPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.5"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.8M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(0.3M, out var pocket2, "Dan");
		pockets.AddPocket(0.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("Dan");
		Assert.Contains(pocket1, output);
		Assert.Contains(pocket2, output);
		Assert.Contains(pocket3, output);
	}

	[Fact]
	public void AutoSelectOnlyKnownByRecipientPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(1.1M, out var pocket2, "Dan");
		pockets.AddPocket(0.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(1.1M, out var pocket4, "David", "Lucas");
		pockets.AddPocket(1.1M, out var pocket5, "David");
		pockets.AddPocket(1.1M, out var pocket6, "Lucas");
		pockets.AddPocket(1.1M, out var pocket7, "David", "Lucas", "Dan");

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets(new SmartLabel("David", "Lucas"));
		Assert.DoesNotContain(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.Contains(pocket4, output);
		Assert.DoesNotContain(pocket5, output);
		Assert.DoesNotContain(pocket6, output);
		Assert.DoesNotContain(pocket7, output);
	}

	[Fact]
	public void AutoSelectOnlyKnownByRecipientPocketsCaseInsensitive()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

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
	public void AutoSelectKnownByRecipientPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.8M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(1.1M, out var pocket2, "Dan");
		pockets.AddPocket(1.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(1.1M, out var pocket4, "David", "Lucas");

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("David");
		Assert.DoesNotContain(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.Contains(pocket4, output);
	}

	[Fact]
	public void AutoSelectKnownByMultipleRecipientPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.8M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(1.1M, out var pocket2, "Dan");
		pockets.AddPocket(0.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(1.1M, out var pocket4, "David", "Lucas", "Dan");
		pockets.AddPocket(1.1M, out var pocket5, "David");
		pockets.AddPocket(1.1M, out var pocket6, "Lucas");
		pockets.AddPocket(1.1M, out var pocket7, "David", "Lucas", "Dan", "Roland");
		pockets.AddPocket(1.1M, out var pocket8, "David", "Dan");

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("David, Lucas");
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
	public void AutoSelectMultipleKnownByMultipleRecipientPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.8M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(1.1M, out var pocket2, "Dan");
		pockets.AddPocket(0.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(0.5M, out var pocket4, "David", "Lucas", "Dan");
		pockets.AddPocket(0.6M, out var pocket5, "David");
		pockets.AddPocket(0.5M, out var pocket6, "Lucas");
		pockets.AddPocket(0.5M, out var pocket7, "David", "Lucas", "Dan", "Roland");
		pockets.AddPocket(0.5M, out var pocket8, "David", "Dan");

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("David, Lucas");
		Assert.DoesNotContain(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.Contains(pocket4, output);
		Assert.Contains(pocket5, output);
		Assert.DoesNotContain(pocket6, output);
		Assert.DoesNotContain(pocket7, output);
		Assert.DoesNotContain(pocket8, output);
	}

	[Fact]
	public void AutoSelectRequiredKnownByRecipientPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(0.8M, out var pocket1, CoinPocketHelper.PrivateFundsText);
		pockets.AddPocket(1.1M, out var pocket2, "Dan");
		pockets.AddPocket(1.5M, out var pocket3, CoinPocketHelper.UnlabelledFundsText);
		pockets.AddPocket(0.2M, out var pocket4, "David");
		pockets.AddPocket(0.5M, out var pocket5, "David", "Lucas");
		pockets.AddPocket(0.6M, out var pocket6, "David", "Lucas", "Dan");

		selection.Reset(pockets.ToArray());

		var output = selection.AutoSelectPockets("David");
		Assert.DoesNotContain(pocket1, output);
		Assert.DoesNotContain(pocket2, output);
		Assert.DoesNotContain(pocket3, output);
		Assert.DoesNotContain(pocket4, output);
		Assert.Contains(pocket5, output);
		Assert.Contains(pocket6, output);
	}

	[Fact]
	public void NotEnoughSelectedWhenSameLabelFoundInSeveralPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

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
}

internal static class LabelTestExtensions
{
	public static LabelViewModel GetLabel(this LabelSelectionViewModel selection, string label)
	{
		return selection.AllLabelsViewModel.Single(x => x.Value == label);
	}

	public static void AddPocket(this List<Pocket> pockets, decimal amount, out Pocket pocket, params string[] labels)
	{
		pocket = new Pocket(new(new SmartLabel(labels), new TestCoinsView(Money.FromUnit(amount, MoneyUnit.BTC))));
		pockets.Add(pocket);
	}
}
