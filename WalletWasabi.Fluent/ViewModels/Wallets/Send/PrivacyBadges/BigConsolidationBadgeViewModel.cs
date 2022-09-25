using System.Linq;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class ConsolidationBadgeViewModel : PrivacyBadgeViewModel
{
	private const int YellowConsolidationThreshold = 10;
	private const int RedConsolidationThreshold = 15;

	public ConsolidationBadgeViewModel()
	{
		BadgeName = "Consolidation";
		Description = "Earn this badge by avoiding to send a high number of coins together";
	}

	public void BuildPrivacySuggestions(Wallet wallet, TransactionInfo info)
	{
		SelectedSuggestion = null;
		Suggestions.Clear();

		var coinCount = info.Coins.Count();

		(Status, Reason) =
			coinCount switch
			{
				>= RedConsolidationThreshold => (PrivacyBadgeStatus.Severe, $"You're sending more than {RedConsolidationThreshold} coins."),
				>= YellowConsolidationThreshold => (PrivacyBadgeStatus.Major, $"You're sending more than {YellowConsolidationThreshold} coins."),
				_ => (PrivacyBadgeStatus.Achieved, "")
			};

		if (Status != Models.PrivacyBadgeStatus.Achieved)
		{
			Suggestions.Add(new SendSmallerAmountSuggestionViewModel());
		}
	}
}
