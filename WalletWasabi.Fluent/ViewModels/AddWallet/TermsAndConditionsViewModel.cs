using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

[NavigationMetaData(Title = "Terms and conditions")]
public partial class TermsAndConditionsViewModel : DialogViewModelBase<bool>
{
	[AutoNotify] private bool _isAgreed;

	private TermsAndConditionsViewModel()
	{
		ViewTermsCommand = ReactiveCommand.Create(() => Navigate().To().LegalDocuments());

		NextCommand = ReactiveCommand.Create(
			OnNext,
			this.WhenAnyValue(x => x.IsAgreed)
				.ObserveOn(RxApp.MainThreadScheduler));

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public ICommand ViewTermsCommand { get; }

	public static async Task<bool> TryShowAsync(UiContext uiContext, IWalletModel walletModel)
	{
		if (walletModel.Auth.IsLegalRequired)
		{
			var accepted = await uiContext.Navigate().To().TermsAndConditions().GetResultAsync();
			if (accepted)
			{
				await walletModel.Auth.AcceptTermsAndConditions();
				return true;
			}
			else
			{
				return false;
			}
		}
		else
		{
			return true;
		}
	}

	private void OnNext()
	{
		Close(DialogResultKind.Normal, true);
	}
}
