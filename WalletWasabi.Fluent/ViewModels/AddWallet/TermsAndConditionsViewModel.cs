using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Terms and conditions")]
public partial class TermsAndConditionsViewModel : DialogViewModelBase<bool>
{
	[AutoNotify] private bool _isAgreed;

	public TermsAndConditionsViewModel(string legalDocument)
	{
		ViewTermsCommand = ReactiveCommand.Create(() => OnViewTerms(legalDocument));

		NextCommand = ReactiveCommand.Create(
			OnNext,
			this.WhenAnyValue(x => x.IsAgreed)
				.ObserveOn(RxApp.MainThreadScheduler));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public ICommand ViewTermsCommand { get; }

	private void OnViewTerms(string legalDocument)
	{
		Navigate().To(new LegalDocumentsViewModel(legalDocument));
	}

	private void OnNext()
	{
		Close(DialogResultKind.Normal, true);
	}
}
