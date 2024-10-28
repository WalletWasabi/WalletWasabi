using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class SendSuccessViewModel : RoutableViewModel
{
	private readonly SmartTransaction _finalTransaction;

	private SendSuccessViewModel(SmartTransaction finalTransaction, string? title = null, string? caption = null)
	{
		_finalTransaction = finalTransaction;
		Title = title ?? Lang.Resources.SendSuccessViewModel_Title;

		Caption = caption ?? Lang.Resources.SendSuccessViewModel_Caption;

		NextCommand = ReactiveCommand.CreateFromTask(OnNextAsync);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	public string? Caption { get; }

	private async Task OnNextAsync()
	{
		await Task.Delay(UiConstants.CloseSuccessDialogMillisecondsDelay);

		Navigate().Clear();

		// TODO: Remove this
		MainViewModel.Instance.NavBar.SelectedWallet?.WalletViewModel?.SelectTransaction(_finalTransaction.GetHash());
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		if (NextCommand is not null && NextCommand.CanExecute(default))
		{
			NextCommand.Execute(default);
		}
	}
}
