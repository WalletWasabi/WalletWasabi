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
	public void Given_max_top_suggestions_the_suggestion_count_should_match(int maxSuggestions, int expectedSuggestionsCount)
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
	public void Top_suggestion_is_taken_removes_it()
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

		Assert.Equal(new [] { "Label 5", "Label 4", "Label 2" }, sut.TopSuggestions);
	}

	[Fact]
	public void No_labels_have_no_suggestions()
	{
		var sut = new SuggestionLabelsViewModel(new TestWallet(new List<(string Label, int Score)>()), Intent.Receive, 5);

		Assert.Empty(sut.Suggestions);
	}

	[Fact]
	public void No_labels_have_no_top_suggestions()
	{
		var sut = new SuggestionLabelsViewModel(new TestWallet(new List<(string Label, int Score)>()), Intent.Receive, 5);

		Assert.Empty(sut.Suggestions);
	}
	
	[Fact]
	public void Suggestions_should_be_in_correct_order_according_to_score()
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
	public void Top_suggestions_should_not_contain_labels_already_chosen()
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
	public void Top_suggestions_should_be_empty_when_all_labels_are_chosen()
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

		public string Name { get; }
		public IObservable<IChangeSet<TransactionSummary, uint256>> Transactions { get; }

		public IObservable<Money> Balance { get; }
		public IObservable<IChangeSet<IAddress, string>> Addresses { get; }

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
