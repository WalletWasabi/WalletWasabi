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

		public LegalDocumentsViewModel(LegalChecker legalChecker)
		{
			Title = "Terms and Conditions";
			NextCommand = BackCommand;
			LegalChecker = legalChecker;

			this.WhenAnyValue(x => x.Content)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(content => IsBusy = content is null);
		}

		private LegalChecker LegalChecker { get; }

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(inStack, disposables);

			Observable.Merge(
				Observable.FromEventPattern<LegalDocuments>(LegalChecker, nameof(LegalChecker.AgreedChanged)),
				Observable.FromEventPattern<LegalDocuments>(LegalChecker, nameof(LegalChecker.ProvisionalChanged)))
				.Select(x => x.EventArgs)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(legalDocuments => Content = legalDocuments.Content)
				.DisposeWith(disposables);

			Content = LegalChecker.TryGetNewLegalDocs(out LegalDocuments? provisional)
				? provisional.Content
				: LegalChecker.CurrentLegalDocument is { } current
					? current.Content
					: null;
		}
	}
}
