using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.SelectWallet;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Services;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent;

public class Bip21Workflow
{
	private readonly UiContext _uiContext;
	private readonly IObservable<WalletViewModel> _currentWallet;
	private string? _uri;
	private IDisposable? _walletSelection;
	private IDisposable? _walletLoading;

	public Bip21Workflow(UiContext uiContext, IObservable<WalletViewModel> currentWallet)
	{
		_uiContext = uiContext;
		_currentWallet = currentWallet;
		
		Observable
			.FromEventPattern<string>(Services.SingleInstanceChecker, nameof(SingleInstanceChecker.UriActivated))
			.Select(static e => e.EventArgs)
			.Where(static e => Uri.TryCreate(e, UriKind.Absolute, out var uri) && uri.Scheme == "bitcoin")
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(Handle);
	}

	public void Handle(string uri)
	{
		_uri = uri;
		_walletSelection ??= _uiContext
			.Navigate()
			.NavigateDialogAsync(new SelectWalletViewModel(), NavigationTarget.DialogScreen)
			.ToObservable()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(result => WalletSelected(result.Result));
	}

	private void WalletSelected(WalletViewModelBase? selectedWallet)
	{
		_walletSelection?.Dispose();
		_walletSelection = null;
		
		if (selectedWallet is { })
		{
			_walletLoading?.Dispose();
			_walletLoading = _currentWallet.Subscribe(
				loadedWallet => WalletLoaded(loadedWallet, selectedWallet.Wallet));

			selectedWallet.OpenCommand.Execute(null);
		}
	}

	private void WalletLoaded(WalletViewModel loadedWallet, Wallet targetWallet)
	{
		_walletLoading?.Dispose();
		_walletLoading = null;

		var uri = _uri;
		
		if (loadedWallet.Wallet == targetWallet &&
			loadedWallet.SendCommand.CanExecute(uri))
		{
			loadedWallet.SendCommand.Execute(uri);
		}
	}
}
