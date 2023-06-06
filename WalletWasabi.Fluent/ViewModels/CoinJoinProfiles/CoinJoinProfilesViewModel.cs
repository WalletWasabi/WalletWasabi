using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

[NavigationMetaData(Title = "Coinjoin Strategy")]
public partial class CoinJoinProfilesViewModel : DialogViewModelBase<bool>
{
	[AutoNotify] private CoinJoinProfileViewModelBase? _selectedProfile;

	private CoinJoinProfilesViewModel(IWalletModel wallet, bool isNewWallet)
	{
		NextCommand = ReactiveCommand.Create(() => OnNext(wallet, isNewWallet));
		EnableBack = true;

		Profiles = DefaultProfiles.ToList();

		ManualSetupCommand = ReactiveCommand.CreateFromTask(OnManualSetupAsync);

		if (isNewWallet)
		{
			_selectedProfile = Profiles[1];
			return;
		}

		_selectedProfile = IdentifySelectedProfile(wallet.Settings);
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
		var dialog = new ManualCoinJoinProfileDialogViewModel(current);

		var dialogResult = await NavigateDialogAsync(dialog, NavigationTarget.CompactDialogScreen);

		if (dialogResult.Result is { } result && result.Profile != current)
		{
			SelectedProfile = null;
			SelectedManualProfile = result.Profile;
		}
	}

	private void OnNext(IWalletModel walletModel, bool isNewWallet)
	{
		var selected = SelectedProfile ?? SelectedManualProfile ?? Profiles.First();

		using (walletModel.Settings.BatchChanges())
		{
			walletModel.Settings.RedCoinIsolation = selected.RedCoinIsolation;
			walletModel.Settings.AnonScoreTarget = selected.AnonScoreTarget;
			walletModel.Settings.FeeRateMedianTimeFrameHours = selected.FeeRateMedianTimeFrameHours;
			walletModel.Settings.IsCoinjoinProfileSelected = true;
		}

		if (isNewWallet)
		{
			// TODO: remove this after AddedWalletPage is decoupled
			var wallet = Services.WalletManager.GetWallets(false).First(x => x.WalletName == walletModel.Name);
			Navigate().To().AddedWalletPage(wallet.KeyManager);
		}
		else
		{
			Close(DialogResultKind.Normal, true);
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false, escapeGoesBack: true);
	}
}
