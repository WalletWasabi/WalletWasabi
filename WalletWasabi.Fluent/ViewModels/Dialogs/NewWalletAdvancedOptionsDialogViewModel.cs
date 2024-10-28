using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class NewWalletAdvancedOptionsDialogViewModel : DialogViewModelBase<NewWalletAdvancedOptionsDialogViewModel.Result>
{
	public NewWalletAdvancedOptionsDialogViewModel(CoinJoinProfileViewModelBase currentProfile, bool isAutoCoinjoinEnabled)
	{
		Title = Lang.Resources.NewWalletAdvancedOptionsDialogViewModel_Title;

		IsAutoCoinjoinEnabled = isAutoCoinjoinEnabled;
		CoinjoinAdvancedSettings = new ManualCoinJoinSettingsViewModel(currentProfile);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		NextCommand = ReactiveCommand.Create(
			() =>
			{
				var isolateRed = CoinjoinAdvancedSettings.RedCoinIsolation;
				var target = CoinjoinAdvancedSettings.AnonScoreTarget;
				var hours = (int)Math.Floor(CoinjoinAdvancedSettings.SelectedTimeFrame.TimeFrame.TotalHours);
				var skipFactors = CoinjoinAdvancedSettings.SkipFactors;

				Close(DialogResultKind.Normal, new Result(new ManualCoinJoinProfileDialogViewModel.ManualCoinJoinProfileDialogViewModelResult(new ManualCoinJoinProfileViewModel(target, hours, isolateRed, skipFactors)), IsAutoCoinjoinEnabled));
			});
	}

	public ManualCoinJoinSettingsViewModel CoinjoinAdvancedSettings { get; }

	public bool IsAutoCoinjoinEnabled { get; set; }

	public record Result(ManualCoinJoinProfileDialogViewModel.ManualCoinJoinProfileDialogViewModelResult CoinjoinProfileResult, bool IsAutoCoinjoinEnabled);
}
