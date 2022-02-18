using ReactiveUI;
using System.Linq;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Manual CoinJoin Profile")]
public partial class ManualCoinJoinProfileDialogViewModel : DialogViewModelBase<ManualCoinJoinProfile?>
{
	[AutoNotify] private string _minAnonScoreTarget;
	[AutoNotify] private string _maxAnonScoreTarget;
	[AutoNotify] private TimeFrameItem[] _timeFrames;
	[AutoNotify] private TimeFrameItem _selectedTimeFrame;

	public ManualCoinJoinProfileDialogViewModel(CoinJoinProfileViewModelBase current)
	{
		_minAnonScoreTarget = current.MinAnonScoreTarget.ToString();
		_maxAnonScoreTarget = current.MaxAnonScoreTarget.ToString();
		_timeFrames = new[]
		{
			new TimeFrameItem("None", TimeSpan.Zero),
			new TimeFrameItem("Daily", TimeSpan.FromHours(Constants.CoinJoinFeeRateAverageTimeFrames[0])),
			new TimeFrameItem("Weekly", TimeSpan.FromHours(Constants.CoinJoinFeeRateAverageTimeFrames[1])),
			new TimeFrameItem("Monthly", TimeSpan.FromHours(Constants.CoinJoinFeeRateAverageTimeFrames[2]))
		};

		_selectedTimeFrame = _timeFrames.FirstOrDefault(tf => tf.TimeFrame == TimeSpan.FromHours(current.FeeRateAverageTimeFrameHours)) ?? _timeFrames.First();

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		EnableBack = false;

		NextCommand = ReactiveCommand.Create(() =>
		{
			var min = int.Parse(MinAnonScoreTarget);
			var max = int.Parse(MaxAnonScoreTarget);
			var hours = (int)Math.Floor(SelectedTimeFrame.TimeFrame.TotalHours);

			Close(DialogResultKind.Normal, new ManualCoinJoinProfile(min, max, hours));
		});

		this.ValidateProperty(x => x.MinAnonScoreTarget, errors => ValidateInteger(MinAnonScoreTarget, nameof(MinAnonScoreTarget), errors));
		this.ValidateProperty(x => x.MaxAnonScoreTarget, errors => ValidateInteger(MaxAnonScoreTarget, nameof(MaxAnonScoreTarget), errors));
	}

	private void ValidateInteger(string text, string propertyName, IValidationErrors errors)
	{
		if (int.TryParse(text, out _))
		{
			return;
		}

		errors.Add(ErrorSeverity.Error, $"Field {propertyName} is not an integer.");
	}

	public record TimeFrameItem(string Name, TimeSpan TimeFrame)
	{
		public override string ToString()
		{
			return Name;
		}
	}
}
