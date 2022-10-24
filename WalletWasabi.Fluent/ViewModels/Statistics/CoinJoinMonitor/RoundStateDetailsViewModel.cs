using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Statistics.CoinJoinMonitor;

[NavigationMetaData(Title = "Round State Details")]
public partial class RoundStateDetailsViewModel : RoutableViewModel
{
	[AutoNotify] private uint256 _id;
	[AutoNotify] private uint256 _blameOf;
	[AutoNotify] private Phase _phase;
	[AutoNotify] private EndRoundState _endRoundState;
	[AutoNotify] private DateTimeOffset _inputRegistrationStart;
	[AutoNotify] private TimeSpan _inputRegistrationTimeout;

	public RoundStateDetailsViewModel(RoundState roundState)
	{
		Id = roundState.Id;
		BlameOf = roundState.BlameOf;

		// TODO: AmountCredentialIssuerParameters

		// TODO: VsizeCredentialIssuerParameters

		Phase = roundState.Phase;
		EndRoundState = roundState.EndRoundState;
		InputRegistrationStart = roundState.InputRegistrationStart;
		InputRegistrationTimeout = roundState.InputRegistrationTimeout;

		// TODO: CoinjoinState
	}
}
