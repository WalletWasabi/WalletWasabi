using ReactiveUI;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Windows.Input;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

[NavigationMetaData(Title = "CoinJoin Profiles")]
public partial class CoinJoinProfilesViewModel : RoutableViewModel
{
	[AutoNotify] private CoinJoinProfileViewModel _selectedProfile;

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

		ManualSetupCommand = ReactiveCommand.Create(OnManualSetup);
	}

	public ICommand ManualSetupCommand { get; }

	public List<CoinJoinProfileViewModel> Profiles { get; }

	private void OnManualSetup()
	{

	}

	private void OnNext(KeyManager keyManager)
	{
		var selected = SelectedProfile;

		keyManager.SetAnonScoreTargets(selected.MinAnonScoreTarget, selected.MaxAnonScoreTarget, toFile: false);
		keyManager.SetFeeTargetAvarageTimeFrame(selected.FeeTargetAvarageTimeFrameHours, toFile: false);

		Navigate().To(new AddedWalletPageViewModel(keyManager));
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}
}
