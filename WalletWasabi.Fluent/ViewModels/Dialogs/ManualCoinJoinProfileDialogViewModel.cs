using ReactiveUI;
using System.Linq;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Coinjoin Profile Settings")]
public partial class ManualCoinJoinProfileDialogViewModel : DialogViewModelBase<ManualCoinJoinProfileDialogViewModel.ManualCoinJoinProfileDialogViewModelResult?>
{
	[AutoNotify] private bool _redCoinIsolation;
	[AutoNotify] private int _anonScoreTarget;
	[AutoNotify] private TimeFrameItem[] _timeFrames;
	[AutoNotify] private TimeFrameItem _selectedTimeFrame;

	public ManualCoinJoinProfileDialogViewModel(CoinJoinProfileViewModelBase current)
	{
		_redCoinIsolation = current.RedCoinIsolation;

		_anonScoreTarget = current.AnonScoreTarget;

		_timeFrames = new[]
		{
			new TimeFrameItem("Hours", TimeSpan.Zero),
			new TimeFrameItem("Days", TimeSpan.FromHours(Constants.CoinJoinFeeRateMedianTimeFrames[0])),
			new TimeFrameItem("Weeks", TimeSpan.FromHours(Constants.CoinJoinFeeRateMedianTimeFrames[1])),
			new TimeFrameItem("Months", TimeSpan.FromHours(Constants.CoinJoinFeeRateMedianTimeFrames[2]))
		};

		_selectedTimeFrame = _timeFrames.FirstOrDefault(tf => tf.TimeFrame == TimeSpan.FromHours(current.FeeRateMedianTimeFrameHours)) ?? _timeFrames.First();

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;

		NextCommand = ReactiveCommand.Create(() =>
		{
			var isolateRed = RedCoinIsolation;
			var target = AnonScoreTarget;
			var hours = (int)Math.Floor(SelectedTimeFrame.TimeFrame.TotalHours);

			Close(DialogResultKind.Normal, new ManualCoinJoinProfileDialogViewModelResult(new ManualCoinJoinProfileViewModel(target, hours, isolateRed)));
		});
	}

	public record TimeFrameItem(string Name, TimeSpan TimeFrame)
	{
		public override string ToString()
		{
			return Name;
		}
	}

	public record ManualCoinJoinProfileDialogViewModelResult(ManualCoinJoinProfileViewModel Profile)
	{
	}
}
