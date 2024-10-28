using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ManualCoinJoinProfileDialogViewModel : DialogViewModelBase<ManualCoinJoinProfileDialogViewModel.ManualCoinJoinProfileDialogViewModelResult?>
{
	public ManualCoinJoinProfileDialogViewModel(CoinJoinProfileViewModelBase current)
	{
		Title = Lang.Resources.ManualCoinJoinProfileDialogViewModel_Title;

		CoinjoinAdvancedSettings = new ManualCoinJoinSettingsViewModel(current);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		NextCommand = ReactiveCommand.Create(
			() =>
			{
				var isolateRed = CoinjoinAdvancedSettings.RedCoinIsolation;
				var target = CoinjoinAdvancedSettings.AnonScoreTarget;
				var hours = (int)Math.Floor(CoinjoinAdvancedSettings.SelectedTimeFrame.TimeFrame.TotalHours);
				var skipFactors = CoinjoinAdvancedSettings.SkipFactors;

				Close(DialogResultKind.Normal, new ManualCoinJoinProfileDialogViewModelResult(new ManualCoinJoinProfileViewModel(target, hours, isolateRed, skipFactors)));
			});
	}

	public ManualCoinJoinSettingsViewModel CoinjoinAdvancedSettings { get; }

	public record ManualCoinJoinProfileDialogViewModelResult(ManualCoinJoinProfileViewModel Profile);
}
