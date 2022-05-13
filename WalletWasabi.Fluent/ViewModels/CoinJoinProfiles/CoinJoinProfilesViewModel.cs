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
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

[NavigationMetaData(Title = "Coinjoin Strategy")]
public partial class CoinJoinProfilesViewModel : DialogViewModelBase<bool>
{
	private readonly KeyManager _keyManager;
	private readonly bool _isNewWallet;
	[AutoNotify] private CoinJoinProfileViewModelBase? _selectedProfile;
	[AutoNotify] private string _plebStopThreshold;

	public CoinJoinProfilesViewModel(KeyManager keyManager, bool isNewWallet)
	{
		_keyManager = keyManager;
		_isNewWallet = isNewWallet;
		_plebStopThreshold = (keyManager.PlebStopThreshold?.ToString() ?? KeyManager.DefaultPlebStopThreshold.ToString()).TrimEnd('0');
		if (_plebStopThreshold.EndsWith('.'))
		{
			_plebStopThreshold = _plebStopThreshold.TrimEnd('.');
		}

		NextCommand = ReactiveCommand.Create(OnNext);
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

		if (_selectedProfile == null && isNewWallet)
		{
			var defaultProfile = Profiles[1];
			_selectedProfile = defaultProfile;
		}

		if (_selectedProfile == null)
		{
			SelectedManualProfile = new ManualCoinJoinProfileViewModel(keyManager.AutoCoinJoin, keyManager.MinAnonScoreTarget, keyManager.MaxAnonScoreTarget, keyManager.FeeRateMedianTimeFrameHours);
		}

		this.WhenAnyValue(x => x.SelectedProfile, x => x.SelectedManualProfile)
			.Subscribe(_ => ApplyChanges());

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
			ApplyChanges();
		}
	}

	private void OnNext()
	{
		ApplyChanges();

		if (_isNewWallet)
		{
			Navigate().To(new AddedWalletPageViewModel(_keyManager));
		}
		else
		{
			_keyManager.ToFile();
			Close(DialogResultKind.Normal, true);
		}
	}

	private void ApplyChanges()
	{
		var selected = SelectedProfile ?? SelectedManualProfile ?? Profiles.First();

		_keyManager.AutoCoinJoin = selected.AutoCoinjoin;
		_keyManager.SetAnonScoreTargets(selected.MinAnonScoreTarget, selected.MaxAnonScoreTarget, toFile: false);
		_keyManager.SetFeeRateMedianTimeFrame(selected.FeeRateMedianTimeFrameHours, toFile: false);
		_keyManager.IsCoinjoinProfileSelected = true;

		if (Money.TryParse(_plebStopThreshold, out Money result) && result != _keyManager.PlebStopThreshold)
		{
			_keyManager.PlebStopThreshold = result;
		}

		if (!_isNewWallet)
		{
			_keyManager.ToFile();
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}
}
