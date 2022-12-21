using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class MusicBoxCoinjoinProfileSelectorViewModel : ActivatableViewModel
{
	[AutoNotify] private CoinJoinProfileViewModelBase? _selectedProfile;
	[AutoNotify] private bool _isSelectorVisible;
	[AutoNotify] private bool _showProfile;
	private readonly KeyManager _keyManager;

	public MusicBoxCoinjoinProfileSelectorViewModel(KeyManager keyManager)
	{
		_keyManager = keyManager;

		Profiles = DefaultProfiles.ToList();
		Profiles.Add(new ManualCoinJoinProfileViewModel(keyManager));

		ShowSelectorCommand = ReactiveCommand.Create(() =>
		{
			IsSelectorVisible = false;
			IsSelectorVisible = true;
		});

		SelectProfileCommand = ReactiveCommand.CreateFromTask<CoinJoinProfileViewModelBase>(async p => await OnCoinjoinProfileSelectedAsync(keyManager, p));
	}

	public ICommand ShowSelectorCommand { get; }
	public ICommand SelectProfileCommand { get; }

	protected override void OnActivated(CompositeDisposable disposables)
	{
		this.WhenAnyValue(x => x.SelectedProfile)
			.Select(x => x is null)
			.CombineLatest(Services.UiConfig.WhenAnyValue(x => x.PrivacyMode))
			.Subscribe(x =>
			{
				Profiles.ForEach(p => p.IsSelected = p.GetType() == SelectedProfile?.GetType());
				ShowProfile = !x.First && !x.Second;
			})
			.DisposeWith(disposables);

		SelectedProfile = IdentifySelectedProfile();
	}

	private async Task OnCoinjoinProfileSelectedAsync(KeyManager keyManager, CoinJoinProfileViewModelBase profile)
	{
		IsSelectorVisible = false;

		if (profile is ManualCoinJoinProfileViewModel)
		{
			var dialog = new ManualCoinJoinProfileDialogViewModel(profile);
			var dialogResultTask = dialog.GetDialogResultAsync();

			NavigationState.Instance.CompactDialogScreenNavigation.To(dialog);

			var dialogResult = await dialogResultTask;

			NavigationState.Instance.CompactDialogScreenNavigation.Clear();

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

	public CoinJoinProfileViewModelBase IdentifySelectedProfile()
	{
		var currentProfile = new ManualCoinJoinProfileViewModel(_keyManager);
		var result = DefaultProfiles.FirstOrDefault(x => x == currentProfile) ?? currentProfile;

		// Edge case: Update the PrivateCJProfile anonscore target, otherwise the randomly selected value will be displayed all time.
		if (result is PrivateCoinJoinProfileViewModel)
		{
			result = new PrivateCoinJoinProfileViewModel(_keyManager.AnonScoreTarget);
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
