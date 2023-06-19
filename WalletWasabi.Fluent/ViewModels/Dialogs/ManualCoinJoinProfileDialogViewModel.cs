using ReactiveUI;
using System.Linq;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Coinjoin Strategy Settings", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class ManualCoinJoinProfileDialogViewModel : DialogViewModelBase<ManualCoinJoinProfileDialogViewModel.ManualCoinJoinProfileDialogViewModelResult?>
{
	[AutoNotify] private bool _redCoinIsolation;
	[AutoNotify] private double _coinjoinProbabilityDaily;
	[AutoNotify] private double _coinjoinProbabilityWeekly;
	[AutoNotify] private double _coinjoinProbabilityMonthly;
	[AutoNotify] private int _anonScoreTarget;

	public ManualCoinJoinProfileDialogViewModel(CoinJoinProfileViewModelBase current)
	{
		_redCoinIsolation = current.RedCoinIsolation;
		_coinjoinProbabilityDaily = current.CoinjoinProbabilityDaily;
		_coinjoinProbabilityWeekly = current.CoinjoinProbabilityWeekly;
		_coinjoinProbabilityMonthly = current.CoinjoinProbabilityMonthly;

		_anonScoreTarget = current.AnonScoreTarget;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		NextCommand = ReactiveCommand.Create(() =>
		{
			var isolateRed = RedCoinIsolation;
			var target = AnonScoreTarget;
			var coinjoinProbabilityDaily = CoinjoinProbabilityDaily;
			var coinjoinProbabilityWeekly = CoinjoinProbabilityWeekly;
			var coinjoinProbabilityMonthly = CoinjoinProbabilityMonthly;

			Close(DialogResultKind.Normal, new ManualCoinJoinProfileDialogViewModelResult(new ManualCoinJoinProfileViewModel(target, isolateRed, coinjoinProbabilityDaily, coinjoinProbabilityWeekly, coinjoinProbabilityMonthly)));
		});
	}

	public record TimeFrameItem(string Name, TimeSpan TimeFrame)
	{
		public override string ToString()
		{
			return Name;
		}
	}

	public record ManualCoinJoinProfileDialogViewModelResult(ManualCoinJoinProfileViewModel Profile);
}
