using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.UserInterfaceTest;

class TestCoinsView : ICoinsView
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
		pockets.AddPocket(1.0M, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, "Target");
		pockets.AddPocket(1.0M, "David", "Adam", "Lucas");
		pockets.AddPocket(1.0M, "Jumar");

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
	public void WhiteListHighlightsAllLabelsInOtherPocketsThatContainTargetLabelExceptThoseAvailableInOtherPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, "Dan");

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
		pockets.AddPocket(1.0M, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, "Dan");

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
		pockets.AddPocket(1.0M, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, "Dan");

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("Dan"));

		Assert.Contains(selection.GetLabel("Target"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Roland"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsBlackList);
	}

	[Fact]
	public void BlackListHighlightsLabelsExclusivelyThatExistInMultiplePockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, "Dan");

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("Dan"));

		Assert.Contains(selection.GetLabel("Target"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Roland"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsBlackList);

		selection.GetLabel("Dan").IsPointerOver = true;

		Assert.True(selection.GetLabel("Dan").IsHighlighted);
		Assert.False(selection.GetLabel("Roland").IsHighlighted);
		Assert.False(selection.GetLabel("Target").IsHighlighted);
	}

	[Fact]
	public void BlackListHighlightsDealWithMultipleOverlapsCorrectly()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, "Target", "Dan");
		pockets.AddPocket(1.0M, "Target");
		pockets.AddPocket(1.0M, "Target", "Roland");

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("Target"));

		Assert.Contains(selection.GetLabel("Target"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Roland"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsBlackList);

		selection.GetLabel("Dan").IsPointerOver = true;

		Assert.True(selection.GetLabel("Dan").IsHighlighted);
		Assert.True(selection.GetLabel("Roland").IsHighlighted);
		Assert.True(selection.GetLabel("Target").IsHighlighted);
	}

	[Fact]
	public void WhiteListHighlightsGroupedLabelsInOtherPocketsThatContainTargetLabelExceptThoseAvailableInOtherPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, "Target", "Dan");
		pockets.AddPocket(1.0M, "Target");
		pockets.AddPocket(1.0M, "Target", "Roland");

		selection.Reset(pockets.ToArray());

		selection.GetLabel("Dan").IsPointerOver = true;

		Assert.False(selection.GetLabel("Target").IsHighlighted);
		Assert.False(selection.GetLabel("Roland").IsHighlighted);
		Assert.True(selection.GetLabel("Dan").IsHighlighted);
	}

	[Fact]
	public void WhiteListSwapsGroupedLabelsInOtherPocketsThatContainTargetLabelExceptThoseAvailableInOtherPockets()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, "Target", "Dan");
		pockets.AddPocket(1.0M, "Target");
		pockets.AddPocket(1.0M, "Target", "Roland");

		selection.Reset(pockets.ToArray());

		selection.SwapLabel(selection.GetLabel("Dan"));

		Assert.DoesNotContain(selection.GetLabel("Target"), selection.LabelsBlackList);
		Assert.DoesNotContain(selection.GetLabel("Roland"), selection.LabelsBlackList);
		Assert.Contains(selection.GetLabel("Dan"), selection.LabelsBlackList);
	}

	[Fact]
	public void Moves_From_WhiteList_Highlights_All_Labels_In_Other_Pockets_That_Contain_TargetLabel()
	{
		var selection = new LabelSelectionViewModel(Money.Parse("1.0"));

		var pockets = new List<Pocket>();
		pockets.AddPocket(1.0M, "Target", "Dan", "Roland");
		pockets.AddPocket(1.0M, "Target");
		pockets.AddPocket(1.0M, "David", "Adam", "Lucas");
		pockets.AddPocket(1.0M, "Jumar");

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
}

internal static class LabelTestExtensions
{
	public static LabelViewModel GetLabel(this LabelSelectionViewModel selection, string label)
	{
		return selection.AllLabelViewModel.Single(x => x.Value == label);
	}

	public static void AddPocket(this List<Pocket> pockets, decimal amount, params string[] labels)
	{
		pockets.Add(new Pocket(new(new SmartLabel(labels), new TestCoinsView(Money.FromUnit(amount, MoneyUnit.BTC)))));
	}
}
