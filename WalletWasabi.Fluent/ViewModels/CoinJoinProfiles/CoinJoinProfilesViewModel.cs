using ReactiveUI;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

[NavigationMetaData(Title = "CoinJoin Profiles")]
public partial class CoinJoinProfilesViewModel : RoutableViewModel
{
	[AutoNotify] private CoinJoinProfileViewModelBase _selectedProfile;

	public CoinJoinProfilesViewModel(KeyManager keyManager)
	{
		NextCommand = ReactiveCommand.Create(() => OnNext(keyManager));
		EnableBack = true;

		var privateProfile = new PrivateCoinJoinProfile();

		Profiles = new()
		{
			privateProfile,
			new SpeedyCoinJoinProfile(),
			new EconomyCoinJoinProfile()
		};

		_selectedProfile = privateProfile;

		ManualSetupCommand = ReactiveCommand.CreateFromTask(async () => await OnManualSetupAsync());
	}

	public ICommand ManualSetupCommand { get; }

	public List<CoinJoinProfileViewModelBase> Profiles { get; }

	private async Task OnManualSetupAsync()
	{
		var dialog = new ManualCoinJoinProfileDialogViewModel();

		var dialogResult = await NavigateDialogAsync(dialog, NavigationTarget.CompactDialogScreen);

		if (dialogResult.Result is { })
		{
		}
	}

	private void OnNext(KeyManager keyManager)
	{
		var selected = SelectedProfile;

		keyManager.SetAnonScoreTargets(selected.MinAnonScoreTarget, selected.MaxAnonScoreTarget, toFile: false);
		keyManager.SetFeeTargetAvarageTimeFrame(selected.FeeTargetAverageTimeFrameHours, toFile: false);

		Navigate().To(new AddedWalletPageViewModel(keyManager));
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}
}
