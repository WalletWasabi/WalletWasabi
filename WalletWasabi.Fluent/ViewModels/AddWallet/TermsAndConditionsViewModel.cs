using System.IO;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Legal;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public partial class TermsAndConditionsViewModel : RoutableViewModel
	{
		[AutoNotify] private bool _isAgreed;

		public TermsAndConditionsViewModel(LegalChecker legalChecker, RoutableViewModel next)
		{
			Title = "Welcome to Wasabi Wallet";

			ViewTermsCommand = ReactiveCommand.Create(
				() =>
				{
					if (legalChecker.IsAgreementRequired(out var legalDocument))
					{
						var legalDocs = new LegalDocumentsViewModel(legalDocument.Content);
						Navigate().To(legalDocs);
					}
				});

			NextCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					await legalChecker.AgreeAsync();
					Navigate().BackTo(next);
				},
				this.WhenAnyValue(x => x.IsAgreed)
				.ObserveOn(RxApp.MainThreadScheduler));
		}

		public ICommand ViewTermsCommand { get; }
	}
}
