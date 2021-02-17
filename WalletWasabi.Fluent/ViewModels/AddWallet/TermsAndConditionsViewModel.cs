using System;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	[NavigationMetaData(Title = "Terms and conditions")]
	public partial class TermsAndConditionsViewModel : DialogViewModelBase<bool>
	{
		[AutoNotify] private bool _isAgreed;

		public TermsAndConditionsViewModel(LegalChecker legalChecker)
		{
			ViewTermsCommand = ReactiveCommand.Create(
				() =>
				{
					if (legalChecker.TryGetNewLegalDocs(out _))
					{
						var legalDocs = new LegalDocumentsViewModel(legalChecker);
						Navigate().To(legalDocs);
					}
				});

			NextCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					await legalChecker.AgreeAsync();
					Close(DialogResultKind.Normal, true);
				},
				this.WhenAnyValue(x => x.IsAgreed)
					.ObserveOn(RxApp.MainThreadScheduler));
		}

		public ICommand ViewTermsCommand { get; }
	}
}
