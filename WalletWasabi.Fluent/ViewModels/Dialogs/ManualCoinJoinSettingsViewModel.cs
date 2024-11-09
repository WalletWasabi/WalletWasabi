using System.Linq;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

public partial class ManualCoinJoinSettingsViewModel : ViewModelBase
{
	[AutoNotify] private bool _redCoinIsolation;
	[AutoNotify] private CoinjoinSkipFactors _skipFactors;
	[AutoNotify] private int _anonScoreTarget;
	[AutoNotify] private TimeFrameItem[] _timeFrames;
	[AutoNotify] private TimeFrameItem _selectedTimeFrame;

	public ManualCoinJoinSettingsViewModel(CoinJoinProfileViewModelBase current)
	{
		_redCoinIsolation = current.RedCoinIsolation;
		_skipFactors = current.SkipFactors;

		_anonScoreTarget = current.AnonScoreTarget;

		_timeFrames =
		[
			new TimeFrameItem(Lang.Utils.Plural("Words_Hour"), TimeSpan.Zero),
			new TimeFrameItem(Lang.Utils.Plural("Words_Day"), TimeSpan.FromHours(Constants.CoinJoinFeeRateMedianTimeFrames[0])),
			new TimeFrameItem(Lang.Utils.Plural("Words_Week"), TimeSpan.FromHours(Constants.CoinJoinFeeRateMedianTimeFrames[1])),
			new TimeFrameItem(Lang.Utils.Plural("Words_Month"), TimeSpan.FromHours(Constants.CoinJoinFeeRateMedianTimeFrames[2]))
		];

		_selectedTimeFrame = _timeFrames.FirstOrDefault(tf => tf.TimeFrame == TimeSpan.FromHours(current.FeeRateMedianTimeFrameHours)) ?? _timeFrames.First();
	}

	public record TimeFrameItem(string Name, TimeSpan TimeFrame)
	{
		public override string ToString()
		{
			return Name;
		}
	}
}
