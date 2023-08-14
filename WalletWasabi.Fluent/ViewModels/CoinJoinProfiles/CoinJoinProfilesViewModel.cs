using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

[NavigationMetaData(Title = "Coinjoin Strategy")]
public partial class CoinJoinProfilesViewModel : DialogViewModelBase<bool>
{
	[AutoNotify] private CoinJoinProfileViewModelBase? _selectedProfile;

	private CoinJoinProfilesViewModel(IWalletSettingsModel walletSettings)
	{
		NextCommand = ReactiveCommand.Create(() => OnNext(walletSettings));
		EnableBack = true;

		Profiles = DefaultProfiles.ToList();

		ManualSetupCommand = ReactiveCommand.CreateFromTask(OnManualSetupAsync);

		_selectedProfile = walletSettings.IsNewWallet ? Profiles[1] : IdentifySelectedProfile(walletSettings);
	}

	private static CoinJoinProfileViewModelBase[] DefaultProfiles { get; } = new CoinJoinProfileViewModelBase[]
	{
			new EconomicCoinJoinProfileViewModel(),
			new SpeedyCoinJoinProfileViewModel(),
			new PrivateCoinJoinProfileViewModel()
	};

	public static CoinJoinProfileViewModelBase IdentifySelectedProfile(IWalletSettingsModel walletSettings)
	{
		var currentProfile = new ManualCoinJoinProfileViewModel(walletSettings);
		var result = DefaultProfiles.FirstOrDefault(x => x == currentProfile) ?? currentProfile;

		// Edge case: Update the PrivateCJProfile anonscore target, otherwise the randomly selected value will be displayed all time.
		if (result is PrivateCoinJoinProfileViewModel)
		{
			result = new PrivateCoinJoinProfileViewModel(walletSettings.AnonScoreTarget);
		}

		return result;
	}

	public ICommand ManualSetupCommand { get; }

	public List<CoinJoinProfileViewModelBase> Profiles { get; }

	public ManualCoinJoinProfileViewModel? SelectedManualProfile { get; private set; }

	private async Task OnManualSetupAsync()
	{
		var current = SelectedProfile ?? SelectedManualProfile ?? Profiles.First();
		var result = await Navigate().To().ManualCoinJoinProfileDialog(current).GetResultAsync();

		if (result is { } && result.Profile != current)
		{
			SelectedProfile = null;
			SelectedManualProfile = result.Profile;
		}
	}

	private void OnNext(IWalletSettingsModel walletSettings)
	{
		var selected = SelectedProfile ?? SelectedManualProfile ?? Profiles.First();
		var isNewWallet = walletSettings.IsNewWallet;

		walletSettings.RedCoinIsolation = selected.RedCoinIsolation;
		walletSettings.AnonScoreTarget = selected.AnonScoreTarget;
		walletSettings.FeeRateMedianTimeFrameHours = selected.FeeRateMedianTimeFrameHours;
		walletSettings.IsCoinjoinProfileSelected = true;

		if (isNewWallet)
		{
			Navigate().To().AddedWalletPage(walletSettings);
		}
		else
		{
			UiContext.WalletRepository.SaveWallet(walletSettings);
			Close(DialogResultKind.Normal, true);
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = UiContext.WalletRepository.HasWallet;
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false, escapeGoesBack: true);
	}
}
