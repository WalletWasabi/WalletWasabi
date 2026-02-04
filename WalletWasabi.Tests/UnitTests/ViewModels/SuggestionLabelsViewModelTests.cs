using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Wallets;
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
		using var disposables = new CompositeDisposable();
		sut.Activate(disposables);

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
		using var disposables = new CompositeDisposable();
		sut.Activate(disposables);

		sut.Labels.Add("Label 3");

		Assert.Equal(new[] { "Label 5", "Label 4", "Label 2" }, sut.TopSuggestions);
	}

	[Fact]
	public void NoLabelsShouldHaveNoSuggestions()
	{
		var sut = new SuggestionLabelsViewModel(new TestWallet(new List<(string Label, int Score)>()), Intent.Receive, 5);
		using var disposables = new CompositeDisposable();
		sut.Activate(disposables);

		Assert.Empty(sut.Suggestions);
	}

	[Fact]
	public void NoLabelsShouldHaveNoTopSuggestions()
	{
		var sut = new SuggestionLabelsViewModel(new TestWallet(new List<(string Label, int Score)>()), Intent.Receive, 5);
		using var disposables = new CompositeDisposable();
		sut.Activate(disposables);

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
		using var disposables = new CompositeDisposable();
		sut.Activate(disposables);

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
		using var disposables = new CompositeDisposable();
		sut.Activate(disposables);

		sut.Labels.Add("Label 1");
		sut.Labels.Add("Label 2");
		sut.Labels.Add("Label 3");

		Assert.Empty(sut.TopSuggestions);
	}

	[Fact]
	public void SuggestionsShouldNotContainDuplicates()
	{
		var labels = new List<(string Label, int Score)>
		{
			("label 1", 1),
			("label 2", 1),
			("label 3", 4),
			("Label 1", 1),
			("Label 2", 2),
			("Label 3", 3),
		};
		var wallet = new TestWallet(labels);
		var sut = new SuggestionLabelsViewModel(wallet, Intent.Send, 100);
		using var disposables = new CompositeDisposable();
		sut.Activate(disposables);

		Assert.Equal(new[] { "label 3", "Label 2", "label 1" }, sut.Suggestions);
	}

	private class TestWallet : IWalletModel
	{
		private readonly List<(string Label, int Score)> _mostUsedLabels;

		public TestWallet(List<(string Label, int Score)> mostUsedLabels)
		{
			_mostUsedLabels = mostUsedLabels;
		}

		// Event required by INotifyPropertyChanged interface but not used in this test mock
#pragma warning disable CS0067
		public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

		public IObservable<bool> IsCoinjoinRunning { get; } = Observable.Return(true);
		public IObservable<bool> IsCoinjoinStarted { get; } = Observable.Return(true);
		public bool IsCoinJoinEnabled { get; } = true;
		public AddressesModel Addresses => throw new NotSupportedException();

		public WalletId Id => throw new NotSupportedException();
		public IEnumerable<ScriptPubKeyType> AvailableScriptPubKeyTypes => throw new NotSupportedException();
		public bool SeveralReceivingScriptTypes { get; }

		public string Name
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public IObservable<bool> Loaded => throw new NotSupportedException();
		bool IWalletModel.IsHardwareWallet => throw new NotSupportedException();
		public bool IsWatchOnlyWallet => throw new NotSupportedException();
		public WalletAuthModel Auth => throw new NotSupportedException();
		public WalletLoadWorkflow Loader => throw new NotSupportedException();
		public IWalletSettingsModel Settings => throw new NotSupportedException();
		public IWalletCoinsModel Coins => throw new NotSupportedException();
		public WalletPrivacyModel Privacy => throw new NotSupportedException();
		public WalletCoinjoinModel Coinjoin => throw new NotSupportedException();
		public Network Network => throw new NotSupportedException();
		WalletTransactionsModel IWalletModel.Transactions => throw new NotSupportedException();
		public IObservable<Amount> Balances => throw new NotSupportedException();
		public IObservable<bool> HasBalance => throw new NotSupportedException();
		public IAmountProvider AmountProvider => throw new NotSupportedException();

		public bool IsLoggedIn { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

		public bool IsLoaded { get; set; }

		public bool IsSelected { get; set; }

		public void Rename(string newWalletName) => throw new NotSupportedException();

		public void Dispose()
		{
		}

		public IEnumerable<(string Label, int Score)> GetMostUsedLabels(Intent intent)
		{
			return _mostUsedLabels;
		}

		public WalletInfoModel GetWalletInfo()
		{
			throw new NotSupportedException();
		}

		public IWalletStatsModel GetWalletStats()
		{
			throw new NotImplementedException();
		}

		public PrivacySuggestionsModel GetPrivacySuggestionsModel(SendFlowModel sendParameters)
		{
			throw new NotImplementedException();
		}
	}
}
