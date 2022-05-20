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
	[AutoNotify] private int _anonScoreTarget;
	[AutoNotify] private TimeFrameItem[] _timeFrames;
	[AutoNotify] private TimeFrameItem _selectedTimeFrame;
	[AutoNotify] private bool _showAutomaticCoinjoin;
	[AutoNotify] private string _plebStopThreshold;

	public ManualCoinJoinProfileDialogViewModel(KeyManager keyManager, CoinJoinProfileViewModelBase current, string plebStopThreshold, bool showAutoCoinjoinToggle)
	{
		_showAutomaticCoinjoin = showAutoCoinjoinToggle && !keyManager.IsWatchOnly;
		_autoCoinjoin = keyManager.AutoCoinJoin;
		_plebStopThreshold = plebStopThreshold;

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

		this.ValidateProperty(x => x.PlebStopThreshold, ValidatePlebStopThreshold);

		var nextCommandCanExecute =
			this.WhenAnyValue(x => x.PlebStopThreshold)
				.Select(x => !Validations.Any);

		NextCommand = ReactiveCommand.Create(() =>
		{
			var auto = AutoCoinjoin;
			var target = AnonScoreTarget;
			var hours = (int)Math.Floor(SelectedTimeFrame.TimeFrame.TotalHours);

			Close(DialogResultKind.Normal, new ManualCoinJoinProfileViewModel(auto, target, hours));
		}, nextCommandCanExecute);
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

	public record TimeFrameItem(string Name, TimeSpan TimeFrame)
	{
		public override string ToString()
		{
			return Name;
		}
	}
}
