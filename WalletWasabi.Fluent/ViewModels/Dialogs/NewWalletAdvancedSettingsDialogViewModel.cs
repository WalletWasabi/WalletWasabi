using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Advanced options", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class NewWalletAdvancedSettingsDialogViewModel : DialogViewModelBase<(bool, ManualCoinJoinProfileDialogViewModel.ManualCoinJoinProfileDialogViewModelResult)>
{
	public NewWalletAdvancedSettingsDialogViewModel(CoinJoinProfileViewModelBase currentProfile, bool isAutoCoinjoinEnabled)
	{
		IsAutoCoinjoinEnabled = isAutoCoinjoinEnabled;
		CoinjoinAdvancedSettings = new ManualCoinJoinSettingsViewModel(currentProfile);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		NextCommand = ReactiveCommand.Create(
			() =>
			{
				var isolateRed = CoinjoinAdvancedSettings.RedCoinIsolation;
				var target = CoinjoinAdvancedSettings.AnonScoreTarget;
				var hours = (int) Math.Floor(CoinjoinAdvancedSettings.SelectedTimeFrame.TimeFrame.TotalHours);
				var skipFactors = CoinjoinAdvancedSettings.SkipFactors;

				Close(DialogResultKind.Normal, (IsAutoCoinjoinEnabled, new ManualCoinJoinProfileDialogViewModel.ManualCoinJoinProfileDialogViewModelResult(new ManualCoinJoinProfileViewModel(target, hours, isolateRed, skipFactors))));
			});
	}

	public ManualCoinJoinSettingsViewModel CoinjoinAdvancedSettings { get; }

	public bool IsAutoCoinjoinEnabled { get; set; }
}
