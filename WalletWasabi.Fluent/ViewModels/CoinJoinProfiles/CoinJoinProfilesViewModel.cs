using NBitcoin;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

[NavigationMetaData(Title = "Coinjoin Strategy")]
public partial class CoinJoinProfilesViewModel : RoutableViewModel
{
	private readonly KeyManager _keyManager;

	[AutoNotify] private CoinJoinProfileViewModelBase? _selectedProfile;
	[AutoNotify] private string _plebStopThreshold;

	public CoinJoinProfilesViewModel(KeyManager keyManager, bool selectDefaultProfile)
	{
		_keyManager = keyManager;
		_plebStopThreshold = (keyManager.PlebStopThreshold?.ToString() ?? KeyManager.DefaultPlebStopThreshold.ToString()).TrimEnd('0');

		NextCommand = ReactiveCommand.Create(() => OnNext(keyManager));
		EnableBack = true;

		Profiles = new()
		{
			new EconomicCoinJoinProfileViewModel(),
			new SpeedyCoinJoinProfileViewModel(),
			new PrivateCoinJoinProfileViewModel()
		};

		_selectedProfile =
			Profiles.Where(x => x.MinAnonScoreTarget == keyManager.MinAnonScoreTarget)
					.Where(x => x.MaxAnonScoreTarget == keyManager.MaxAnonScoreTarget)
					.Where(x => x.FeeRateMedianTimeFrameHours == keyManager.FeeRateMedianTimeFrameHours)
					.FirstOrDefault();

		if (_selectedProfile == null && selectDefaultProfile)
		{
			_selectedProfile = Profiles[1];
		}

		if (_selectedProfile == null)
		{
			SelectedManualProfile = new ManualCoinJoinProfileViewModel(keyManager.AutoCoinJoin, keyManager.MinAnonScoreTarget, keyManager.MaxAnonScoreTarget, keyManager.FeeRateMedianTimeFrameHours);
		}

		ManualSetupCommand = ReactiveCommand.CreateFromTask(async () => await OnManualSetupAsync());
	}

	public ICommand ManualSetupCommand { get; }

	public List<CoinJoinProfileViewModelBase> Profiles { get; }

	public ManualCoinJoinProfileViewModel? SelectedManualProfile { get; private set; }

	private async Task OnManualSetupAsync()
	{
		var current = SelectedProfile ?? SelectedManualProfile ?? Profiles.First();
		var dialog = new ManualCoinJoinProfileDialogViewModel(_keyManager, current, _plebStopThreshold);

		var dialogResult = await NavigateDialogAsync(dialog, NavigationTarget.CompactDialogScreen);

		if (dialogResult.Result is ManualCoinJoinProfileViewModel profile)
		{
			SelectedProfile = null;
			SelectedManualProfile = profile;
			_plebStopThreshold = dialog.PlebStopThreshold;
		}
	}

	private void OnNext(KeyManager keyManager)
	{
		var selected = SelectedProfile ?? SelectedManualProfile ?? Profiles.First();

		keyManager.AutoCoinJoin = selected.AutoCoinjoin;
		keyManager.SetAnonScoreTargets(selected.MinAnonScoreTarget, selected.MaxAnonScoreTarget, toFile: false);
		keyManager.SetFeeRateMedianTimeFrame(selected.FeeRateMedianTimeFrameHours, toFile: false);

		if (Money.TryParse(_plebStopThreshold, out Money result) && result != _keyManager.PlebStopThreshold)
		{
			_keyManager.PlebStopThreshold = result;
		}

		Navigate().To(new AddedWalletPageViewModel(keyManager));
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}
}
