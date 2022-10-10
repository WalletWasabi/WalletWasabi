using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.Fluent.ViewModels.Statistics;

[NavigationMetaData(Title = "Round State")]
public partial class RoundStateViewModel : RoutableViewModel
{
	[AutoNotify] private uint256 _id;
	[AutoNotify] private uint256 _blameOf;
	[AutoNotify] private Phase _phase;
	[AutoNotify] private EndRoundState _endRoundState;
	[AutoNotify] private DateTimeOffset _inputRegistrationStart;
	[AutoNotify] private TimeSpan _inputRegistrationTimeout;

	public RoundStateViewModel(RoundState roundState)
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
