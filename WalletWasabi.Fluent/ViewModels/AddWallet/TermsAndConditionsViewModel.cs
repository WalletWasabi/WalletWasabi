using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	[NavigationMetaData(Title = "Terms and conditions")]
	public partial class TermsAndConditionsViewModel : DialogViewModelBase<bool>
	{
		[AutoNotify] private bool _isAgreed;

		public TermsAndConditionsViewModel(string legalDocument)
		{
			ViewTermsCommand = ReactiveCommand.Create(() => ViewTermsExecute(legalDocument));

			NextCommand = ReactiveCommand.Create(
				NextExecute,
				this.WhenAnyValue(x => x.IsAgreed)
					.ObserveOn(RxApp.MainThreadScheduler));
		}

		private void ViewTermsExecute(string legalDocument)
		{
			Navigate().To(new LegalDocumentsViewModel(legalDocument));
		}

		private void NextExecute()
		{
			Close(DialogResultKind.Normal, true);
		}

		public ICommand ViewTermsCommand { get; }
	}
}
