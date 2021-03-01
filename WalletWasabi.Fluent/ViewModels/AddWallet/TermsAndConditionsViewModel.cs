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
			ViewTermsCommand = ReactiveCommand.Create(
				() => Navigate().To(new LegalDocumentsViewModel(legalDocument)));

			NextCommand = ReactiveCommand.Create(
				() => Close(DialogResultKind.Normal, true),
				this.WhenAnyValue(x => x.IsAgreed)
					.ObserveOn(RxApp.MainThreadScheduler));
		}

		public ICommand ViewTermsCommand { get; }
	}
}
