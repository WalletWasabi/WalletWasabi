using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

[NavigationMetaData(Title = "Coinjoin Profiles")]
public partial class MusicBoxCoinjoinProfileSelectorViewModel : RoutableViewModel
{
	[AutoNotify] private CoinJoinProfileViewModelBase? _selectedProfile;
	[AutoNotify] private bool _isFlyoutOpen;
	[AutoNotify] private bool _showProfile;

	public MusicBoxCoinjoinProfileSelectorViewModel(KeyManager keyManager)
	{
		Profiles = DefaultProfiles.ToList();
		Profiles.Add(new ManualCoinJoinProfileViewModel(keyManager));

		_selectedProfile = IdentifySelectedProfile(keyManager);

		OpenFlyoutCommand = ReactiveCommand.Create(() =>
		{
			IsFlyoutOpen = false;
			IsFlyoutOpen = true;
		});
		SelectProfileCommand = ReactiveCommand.CreateFromTask<CoinJoinProfileViewModelBase>(async p => await OnCoinjoinProfileSelectedAsync(keyManager, p));

		this.WhenAnyValue(x => x.SelectedProfile)
			.Select(x => x is null)
			.CombineLatest(Services.UiConfig.WhenAnyValue(x => x.PrivacyMode))
			.Subscribe(x =>
			{
				Profiles.ForEach(p => p.IsSelected = p.GetType() == SelectedProfile?.GetType());
				ShowProfile = !x.First && !x.Second;
			});
	}

	public ICommand OpenFlyoutCommand { get; }
	public ICommand SelectProfileCommand { get; }

	private async Task OnCoinjoinProfileSelectedAsync(KeyManager keyManager, CoinJoinProfileViewModelBase profile)
	{
		IsFlyoutOpen = false;

		if (profile is ManualCoinJoinProfileViewModel)
		{
			var dialog = new ManualCoinJoinProfileDialogViewModel(profile);

			var dialogResult = await NavigateDialogAsync(dialog, NavigationTarget.CompactDialogScreen);

			if (dialogResult.Result is { } result)
			{
				profile = result.Profile;
			}
			else
			{
				return;
			}
		}

		SelectedProfile = null;
		SelectedProfile = profile;
		keyManager.SetCoinjoinProfile(profile);
	}

	public static CoinJoinProfileViewModelBase IdentifySelectedProfile(KeyManager keyManager)
	{
		var currentProfile = new ManualCoinJoinProfileViewModel(keyManager);
		var result = DefaultProfiles.FirstOrDefault(x => x == currentProfile) ?? currentProfile;

		// Edge case: Update the PrivateCJProfile anonscore target, otherwise the randomly selected value will be displayed all time.
		if (result is PrivateCoinJoinProfileViewModel)
		{
			result = new PrivateCoinJoinProfileViewModel(keyManager.AnonScoreTarget);
		}

		return result;
	}

	private static CoinJoinProfileViewModelBase[] DefaultProfiles { get; } = new CoinJoinProfileViewModelBase[]
	{
		new PrivateCoinJoinProfileViewModel(),
		new SpeedyCoinJoinProfileViewModel(),
		new EconomicCoinJoinProfileViewModel(),
	};

	public List<CoinJoinProfileViewModelBase> Profiles { get; }
}
