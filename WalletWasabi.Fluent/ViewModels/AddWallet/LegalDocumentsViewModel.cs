using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Legal;
using WalletWasabi.Services;
using System;
using System.Reactive.Linq;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	[NavigationMetaData(
		Title = "Legal Docs",
		Caption = "Displays terms and conditions",
		Order = 3,
		Category = "General",
		Keywords = new[] { "View", "Legal", "Docs", "Documentation", "Terms", "Conditions", "Help" },
		IconName = "info_regular",
		NavigationTarget = NavigationTarget.DialogScreen)]
	public partial class LegalDocumentsViewModel : RoutableViewModel
	{
		[AutoNotify] private string? _content;

		public LegalDocumentsViewModel(LegalChecker legalChecker)
		{
			Title = "Terms and Conditions";
			NextCommand = BackCommand;

			Observable
				.FromEventPattern<LegalDocuments>(legalChecker, nameof(LegalChecker.ProvisionalChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (x.EventArgs is LegalDocuments legalDocuments)
					{
						Content = legalDocuments.Content;
					}
				});

			Observable
				.FromEventPattern<LegalDocuments>(legalChecker, nameof(LegalChecker.AgreedChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (x.EventArgs is LegalDocuments legalDocuments)
					{
						Content = legalDocuments.Content;
					}
				});

			if (legalChecker.TryGetNewLegalDocs(out LegalDocuments? provisional))
			{
				Content = provisional.Content;
			}
			else if (legalChecker.CurrentLegalDocument is { } current)
			{
				Content = current.Content;
			}
			else
			{
				//TODO: display busy logic.
				Content = "Loading...";
			}
		}
	}
}
