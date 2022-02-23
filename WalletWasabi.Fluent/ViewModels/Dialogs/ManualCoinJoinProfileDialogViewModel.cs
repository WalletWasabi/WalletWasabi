using ReactiveUI;
using System.Linq;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Coinjoin Settings")]
public partial class ManualCoinJoinProfileDialogViewModel : DialogViewModelBase<ManualCoinJoinProfile?>
{
	[AutoNotify] private bool _autoCoinjoin;
	[AutoNotify] private string _minAnonScoreTarget;
	[AutoNotify] private string _maxAnonScoreTarget;
	[AutoNotify] private TimeFrameItem[] _timeFrames;
	[AutoNotify] private TimeFrameItem _selectedTimeFrame;

	public ManualCoinJoinProfileDialogViewModel(CoinJoinProfileViewModelBase current)
	{
		_autoCoinjoin = true;
		_minAnonScoreTarget = current.MinAnonScoreTarget.ToString();
		_maxAnonScoreTarget = current.MaxAnonScoreTarget.ToString();
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
			var auto = AutoCoinjoin;
			var min = int.Parse(MinAnonScoreTarget);
			var max = int.Parse(MaxAnonScoreTarget);
			var hours = (int)Math.Floor(SelectedTimeFrame.TimeFrame.TotalHours);

			Close(DialogResultKind.Normal, new ManualCoinJoinProfile(auto, min, max, hours));
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
