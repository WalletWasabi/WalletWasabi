using ReactiveUI;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Keys;
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
	[AutoNotify] private bool _showAutomaticCoinjoin;
	[AutoNotify] private string _plebStopThreshold;
	[AutoNotify] private int _plebStopThresholdFactor;

	public ManualCoinJoinProfileDialogViewModel(KeyManager keyManager, CoinJoinProfileViewModelBase current, string plebStopThreshold)
	{
		_showAutomaticCoinjoin = !keyManager.IsWatchOnly;
		_autoCoinjoin = keyManager.AutoCoinJoin;
		_plebStopThreshold = plebStopThreshold;
		_plebStopThresholdFactor = _plebStopThreshold.Split('.')[1].TakeWhile(x => x == '0').Count();

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

		this.WhenAnyValue(x => x.PlebStopThresholdFactor)
			.Skip(1)
			.Subscribe(x => PlebStopThreshold = (1 / (decimal)Math.Pow(10.0, x + 1)).ToString("N5").TrimEnd('0'));

		this.ValidateProperty(x => x.PlebStopThreshold, ValidatePlebStopThreshold);

		var nextCommandCanExecute =
			this.WhenAnyValue(x => x.PlebStopThreshold)
				.Select(x => !Validations.Any);

		NextCommand = ReactiveCommand.Create(() =>
		{
			var auto = AutoCoinjoin;
			var min = MinAnonScoreTarget;
			var max = MaxAnonScoreTarget;
			var hours = (int)Math.Floor(SelectedTimeFrame.TimeFrame.TotalHours);

			Close(DialogResultKind.Normal, new ManualCoinJoinProfileViewModel(auto, min, max, hours));
		}, nextCommandCanExecute);
	}

	public record TimeFrameItem(string Name, TimeSpan TimeFrame)
	{
		public override string ToString()
		{
			return Name;
		}
	}

	private void ValidatePlebStopThreshold(IValidationErrors errors)
	{
		if (PlebStopThreshold.Contains(',', StringComparison.InvariantCultureIgnoreCase))
		{
			errors.Add(ErrorSeverity.Error, "Use decimal point instead of comma.");
		}
		else if (!decimal.TryParse(PlebStopThreshold, out _))
		{
			errors.Add(ErrorSeverity.Error, "Invalid coinjoin threshold.");
		}
	}
}
