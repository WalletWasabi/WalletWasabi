using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Terms and conditions")]
public partial class TermsAndConditionsViewModel : DialogViewModelBase<bool>
{
	[ObservableProperty] [NotifyCanExecuteChangedFor(nameof(NextCommand))]
	private bool _isAgreed;

	public TermsAndConditionsViewModel()
	{
		ViewTermsCommand = new RelayCommand(() => Navigate().To(new LegalDocumentsViewModel()));

		NextCommand = new RelayCommand(OnNext, () => IsAgreed);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public ICommand ViewTermsCommand { get; }

	private void OnNext()
	{
		Close(DialogResultKind.Normal, true);
	}
}
