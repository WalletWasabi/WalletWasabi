using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[NavigationMetaData(Title = null)]
public partial class LoadingViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	[AutoNotify] private bool _autoCoinJoin;
	[AutoNotify] private bool _isNewWallet;
	[AutoNotify] private double _percent;
	[AutoNotify] private string _statusText = " "; // Should not be empty as we have to preserve the space in the view.
	[AutoNotify] private bool _isLoading;

	public ICommand ToggleAutoCoinJoinCommand { get; }

	public LoadingViewModel(IWalletModel wallet)
	{
		_wallet = wallet;
		_autoCoinJoin = _wallet.Settings.AutoCoinjoin;
		_isNewWallet = true;

		ToggleAutoCoinJoinCommand = ReactiveCommand.Create(() =>
		{
			UpdateAutoCoinjoinProperty();
		});
	}

	public string WalletName => _wallet.Name;

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		_wallet.Loader.Progress
					  .Do(p => UpdateStatus(p.PercentComplete, p.TimeRemaining))
					  .Subscribe()
					  .DisposeWith(disposables);
	}

	private void UpdateAutoCoinjoinProperty()
	{
		if (_wallet.Settings.IsCoinjoinProfileSelected)
		{
			_wallet.Settings.AutoCoinjoin = AutoCoinJoin;
			_wallet.Settings.Save();
		}
		else
		{
			AutoCoinJoin = false;
		}
	}

	private void UpdateStatus(double percent, TimeSpan remainingTimeSpan)
	{
		Percent = percent;
		var percentText = $"{Percent}% completed";

		var userFriendlyTime = TextHelpers.TimeSpanToFriendlyString(remainingTimeSpan);
		var remainingTimeText = string.IsNullOrEmpty(userFriendlyTime) ? "" : $"- {userFriendlyTime} remaining";

		StatusText = $"{percentText} {remainingTimeText}";
	}
}
