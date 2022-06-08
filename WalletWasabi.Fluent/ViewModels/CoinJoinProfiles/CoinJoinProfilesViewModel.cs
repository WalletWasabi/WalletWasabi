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
	[AutoNotify] private CoinJoinProfileViewModelBase? _selectedProfile;
	[AutoNotify] private bool _autoCoinJoin;

	public CoinJoinProfilesViewModel(KeyManager keyManager, bool isNewWallet)
	{
		NextCommand = ReactiveCommand.Create(() => OnNext(keyManager, isNewWallet));
		EnableBack = true;

		AutoCoinJoin = keyManager.AutoCoinJoin;

		Profiles = new()
		{
			new EconomicCoinJoinProfileViewModel(),
			new SpeedyCoinJoinProfileViewModel(),
			new PrivateCoinJoinProfileViewModel()
		};

		if (isNewWallet)
		{
			_selectedProfile = Profiles[1];
		}
		else
		{
			_selectedProfile = Profiles.FirstOrDefault(x => x.Title == keyManager.CoinjoinProfile);

			if (_selectedProfile is PrivateCoinJoinProfileViewModel p)
			{
				Profiles.Remove(p);
				_selectedProfile = new PrivateCoinJoinProfileViewModel(keyManager.AnonScoreTarget);
				Profiles.Add(_selectedProfile);
			}

			if (_selectedProfile is null && keyManager.CoinjoinProfile is { })
			{
				SelectedManualProfile = new ManualCoinJoinProfileViewModel(keyManager.AnonScoreTarget, keyManager.FeeRateMedianTimeFrameHours);
			}
		}

		ManualSetupCommand = ReactiveCommand.CreateFromTask(async () => await OnManualSetupAsync());
	}

	public ICommand ManualSetupCommand { get; }

	public List<CoinJoinProfileViewModelBase> Profiles { get; }

	public ManualCoinJoinProfileViewModel? SelectedManualProfile { get; private set; }

	private async Task OnManualSetupAsync()
	{
		var current = SelectedProfile ?? SelectedManualProfile ?? Profiles.First();
		var dialog = new ManualCoinJoinProfileDialogViewModel(current);

		var dialogResult = await NavigateDialogAsync(dialog, NavigationTarget.CompactDialogScreen);

		if (dialogResult.Result is ManualCoinJoinProfileDialogViewModel.ManualCoinJoinProfileDialogViewModelResult result)
		{
			SelectedProfile = null;
			SelectedManualProfile = result.Profile;
			AutoCoinJoin = result.AutoCoinJoin;
		}
	}

	private void OnNext(KeyManager keyManager, bool isNewWallet)
	{
		var selected = SelectedProfile ?? SelectedManualProfile ?? Profiles.First();

		keyManager.AutoCoinJoin = AutoCoinJoin;
		keyManager.SetAnonScoreTarget(selected.AnonScoreTarget, toFile: false);
		keyManager.SetFeeRateMedianTimeFrame(selected.FeeRateMedianTimeFrameHours, toFile: false);
		keyManager.IsCoinjoinProfileSelected = true;
		keyManager.CoinjoinProfile = selected.Title;

		if (isNewWallet)
		{
			Navigate().To(new AddedWalletPageViewModel(keyManager));
		}
		else
		{
			keyManager.ToFile();
			Close(DialogResultKind.Normal, true);
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var enableCancel = Services.WalletManager.HasWallet();
		SetupCancel(enableCancel: false, enableCancelOnEscape: enableCancel, enableCancelOnPressed: false);
	}
}
