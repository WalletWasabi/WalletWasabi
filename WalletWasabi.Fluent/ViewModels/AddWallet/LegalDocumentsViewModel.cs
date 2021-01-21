using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Legal;
using WalletWasabi.Services;
using System;
using System.Reactive.Linq;
using System.Reactive.Disposables;

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

		private LegalChecker LegalChecker { get; }

		public LegalDocumentsViewModel(LegalChecker legalChecker)
		{
			Title = "Terms and Conditions";
			NextCommand = BackCommand;
			LegalChecker = legalChecker;
		}

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			Observable.Merge(
				Observable.FromEventPattern<LegalDocuments>(LegalChecker, nameof(LegalChecker.AgreedChanged)),
				Observable.FromEventPattern<LegalDocuments>(LegalChecker, nameof(LegalChecker.ProvisionalChanged)))
				.Select(x => x.EventArgs)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(legalDocuments => UpdateContent(legalDocuments))
				.DisposeWith(disposable);

			if (LegalChecker.TryGetNewLegalDocs(out LegalDocuments? provisional))
			{
				UpdateContent(provisional);
			}
			else if (LegalChecker.CurrentLegalDocument is { } current)
			{
				UpdateContent(current);
			}
			else
			{
				//TODO: display busy logic.
				Content = "Loading...";
			}
		}

		private void UpdateContent(LegalDocuments legalDocuments)
		{
			Content = legalDocuments.Content;
		}
	}
}
