using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Coinjoin Strategy Settings", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ManualCoinJoinProfileDialogViewModel : DialogViewModelBase<ManualCoinJoinProfileDialogViewModel.ManualCoinJoinProfileDialogViewModelResult?>
{
	public ManualCoinJoinProfileDialogViewModel(CoinJoinProfileViewModelBase current)
	{
		CoinjoinAdvancedSettings = new ManualCoinJoinSettingsViewModel(current);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		NextCommand = ReactiveCommand.Create(
			() =>
			{
				var isolateRed = CoinjoinAdvancedSettings.RedCoinIsolation;
				var target = CoinjoinAdvancedSettings.AnonScoreTarget;
				var hours = (int)Math.Floor(CoinjoinAdvancedSettings.SelectedTimeFrame.TimeFrame.TotalHours);

				Close(DialogResultKind.Normal, new ManualCoinJoinProfileDialogViewModelResult(new ManualCoinJoinProfileViewModel(target, hours, isolateRed)));
			});
	}

	public ManualCoinJoinSettingsViewModel CoinjoinAdvancedSettings { get; }

	public record ManualCoinJoinProfileDialogViewModelResult(ManualCoinJoinProfileViewModel Profile);
}
