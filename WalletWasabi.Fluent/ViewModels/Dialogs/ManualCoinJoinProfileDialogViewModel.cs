using ReactiveUI;
using System.Linq;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Coinjoin Settings")]
public partial class ManualCoinJoinProfileDialogViewModel : DialogViewModelBase<ManualCoinJoinProfileViewModel?>
{
	[AutoNotify] private bool _autoCoinjoin;
	[AutoNotify] private int _minAnonScoreTarget;
	[AutoNotify] private int _maxAnonScoreTarget;
	[AutoNotify] private TimeFrameItem[] _timeFrames;
	[AutoNotify] private TimeFrameItem _selectedTimeFrame;

	public ManualCoinJoinProfileDialogViewModel(CoinJoinProfileViewModelBase current)
	{
		_autoCoinjoin = true;

		_minAnonScoreTarget = current.MinAnonScoreTarget;
		_maxAnonScoreTarget = current.MaxAnonScoreTarget;

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

		this.WhenAnyValue(x => x.MinAnonScoreTarget)
			.Subscribe(
				x =>
				{
					if (x >= MaxAnonScoreTarget)
					{
						MaxAnonScoreTarget = x + 1;
					}
				});

		this.WhenAnyValue(x => x.MaxAnonScoreTarget)
			.Subscribe(
				x =>
				{
					if (x <= MinAnonScoreTarget)
					{
						MinAnonScoreTarget = x - 1;
					}
				});


		NextCommand = ReactiveCommand.Create(() =>
		{
			var auto = AutoCoinjoin;
			var min = MinAnonScoreTarget;
			var max = MaxAnonScoreTarget;
			var hours = (int)Math.Floor(SelectedTimeFrame.TimeFrame.TotalHours);

			Close(DialogResultKind.Normal, new ManualCoinJoinProfileViewModel(auto, min, max, hours));
		});
	}

	public record TimeFrameItem(string Name, TimeSpan TimeFrame)
	{
		public override string ToString()
		{
			return Name;
		}
	}
}
