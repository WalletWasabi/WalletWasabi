using ReactiveUI;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class AnonymityBadgeViewModel : PrivacyBadgeViewModel
{
	public AnonymityBadgeViewModel()
	{
		BadgeName = "Anonymity";
		Description = "Earn this badge by sending anonymized funds";
	}

	public void BuildPrivacySuggestions(Wallet wallet, TransactionInfo info)
	{
		SelectedSuggestion = null;
		Suggestions.Clear();

		var coins = info.Coins;

		var hasRedCoins =
			coins.Any(x => x.GetPrivacyLevel(wallet) == PrivacyLevel.NonPrivate);

		var hasYellowCoins =
			coins.Any(x => x.GetPrivacyLevel(wallet) == PrivacyLevel.SemiPrivate);

		var hasGreenCoins =
			coins.Any(x => x.GetPrivacyLevel(wallet) == PrivacyLevel.Private);

		(Status, Reason) =
			(hasRedCoins, hasYellowCoins, hasGreenCoins) switch
			{
				(true, true, true) => (PrivacyBadgeStatus.Severe, "you're mixing red (non-private), yellow (semi-private) and green (private) coins."),

				(true, false, false) => (PrivacyBadgeStatus.Major, "you're sending red (non-private) coins."),
				(true, true, false) => (PrivacyBadgeStatus.Severe, "you're mixing red (non-private) and yellow (semi-private) coins."),
				(true, false, true) => (PrivacyBadgeStatus.Severe, "you're mixing red (non-private) and green (private) coins."),

				(false, true, false) => (PrivacyBadgeStatus.Minor, "you're sending yellow (semi-private) coins."),
				(false, true, true) => (PrivacyBadgeStatus.Minor, "you're mixing yellow (semi-private) and green (private) coins."),

				(false, false, true) => (PrivacyBadgeStatus.Achieved, "you're sending green (private) coins."),
				_ => throw new InvalidOperationException($"this should never happen")
			};

		if (Status != PrivacyBadgeStatus.Achieved)
		{
			Suggestions.Add(new CoinjoinSuggestionViewModel());
		}
	}
}
