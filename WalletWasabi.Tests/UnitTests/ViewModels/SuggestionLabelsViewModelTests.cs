using System.Collections.Generic;
using DynamicData;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class SuggestionLabelsViewModelTests
{
	[InlineData(1, 1)]
	[InlineData(2, 2)]
	[InlineData(3, 3)]
	[InlineData(100, 5)]
	[Theory]
	public void GivenMaxTopSuggestionsTheSuggestionCountShouldMatch(int maxSuggestions, int expectedSuggestionsCount)
	{
		var wallet = new TestWallet(
			new List<(string Label, int Score)>
			{
				("Label 1", 1),
				("Label 2", 2),
				("Label 3", 3),
				("Label 4", 5),
				("Label 5", 4)
			});
		var sut = new SuggestionLabelsViewModel(wallet, Intent.Send, maxSuggestions);

		Assert.Equal(expectedSuggestionsCount, sut.TopSuggestions.Count);
	}

	[Fact]
	public void WhenLabelIsTakenItShouldNotBeSuggested()
	{
		var wallet = new TestWallet(
			new List<(string Label, int Score)>
			{
				("Label 1", 1),
				("Label 2", 2),
				("Label 3", 3),
				("Label 4", 4),
				("Label 5", 5),
			});

		var sut = new SuggestionLabelsViewModel(wallet, Intent.Send, 3);

		sut.Labels.Add("Label 3");

		Assert.Equal(new[] { "Label 5", "Label 4", "Label 2" }, sut.TopSuggestions);
	}

	[Fact]
	public void NoLabelsShouldHaveNoSuggestions()
	{
		var sut = new SuggestionLabelsViewModel(new TestWallet(new List<(string Label, int Score)>()), Intent.Receive, 5);

		Assert.Empty(sut.Suggestions);
	}

	[Fact]
	public void NoLabelsShouldHaveNoTopSuggestions()
	{
		var sut = new SuggestionLabelsViewModel(new TestWallet(new List<(string Label, int Score)>()), Intent.Receive, 5);

		Assert.Empty(sut.Suggestions);
	}

	[Fact]
	public void SuggestionsShouldBeInCorrectOrderAccordingToScore()
	{
		var mostUsedLabels = new List<(string Label, int Score)>
		{
			("Label 1", 1),
			("Label 2", 3),
			("Label 3", 2),
		};
		var wallet = new TestWallet(mostUsedLabels);
		var sut = new SuggestionLabelsViewModel(wallet, Intent.Send, 100);

		Assert.Equal(new[] { "Label 2", "Label 3", "Label 1" }, sut.Suggestions);
	}

	[Fact]
	public void Suggestions_should_not_contain_labels_already_chosen()
	{
		var mostUsedLabels = new List<(string Label, int Score)>
		{
			("Label 1", 1),
			("Label 2", 3),
			("Label 3", 2),
		};
		var wallet = new TestWallet(mostUsedLabels);
		var sut = new SuggestionLabelsViewModel(wallet, Intent.Send, 100);

		sut.Labels.Add("Label 3");

		Assert.DoesNotContain("Label 3, ", sut.Suggestions);
	}

	[Fact]
	public void SuggestionsShouldNotContainLabelsAlreadyChosen()
	{
		var mostUsedLabels = new List<(string Label, int Score)>
		{
			("Label 1", 1),
			("Label 2", 3),
			("Label 3", 2),
		};
		var wallet = new TestWallet(mostUsedLabels);
		var sut = new SuggestionLabelsViewModel(wallet, Intent.Send, 100);

		sut.Labels.Add("Label 3");
		sut.Labels.Add("Label 1");

		Assert.DoesNotContain("Label 3", sut.TopSuggestions);
		Assert.DoesNotContain("Label 1", sut.TopSuggestions);
	}

	[Fact]
	public void TopSuggestionsShouldBeEmptyWhenAllLabelsAreChosen()
	{
		var mostUsedLabels = new List<(string Label, int Score)>
		{
			("Label 1", 1),
			("Label 2", 3),
			("Label 3", 2),
		};
		var wallet = new TestWallet(mostUsedLabels);
		var sut = new SuggestionLabelsViewModel(wallet, Intent.Send, 1);

		sut.Labels.Add("Label 1");
		sut.Labels.Add("Label 2");
		sut.Labels.Add("Label 3");

		Assert.Empty(sut.TopSuggestions);
	}

	private class TestWallet : IWalletModel
	{
		private readonly List<(string Label, int Score)> _mostUsedLabels;

		public TestWallet(List<(string Label, int Score)> mostUsedLabels)
		{
			_mostUsedLabels = mostUsedLabels;
		}

		public string Name => throw new NotSupportedException();
		public IObservable<IChangeSet<TransactionSummary, uint256>> Transactions => throw new NotSupportedException();
		public IObservable<Money> Balance => throw new NotSupportedException();
		public IObservable<IChangeSet<IAddress, string>> Addresses => throw new NotSupportedException();

		public IAddress GetNextReceiveAddress(IEnumerable<string> destinationLabels)
		{
			throw new NotSupportedException();
		}

		public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
		{
			return _mostUsedLabels;
		}

		public bool IsHardwareWallet()
		{
			throw new NotSupportedException();
		}
	}
}
