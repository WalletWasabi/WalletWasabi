namespace WalletWasabi.WabiSabi.Models;

public record HumanMonitorRoundResponse(
	uint256 RoundId,
	bool IsBlameRound,
	int InputCount,
	decimal MaxSuggestedAmount,
	TimeSpan InputRegistrationRemaining,
	string Phase);
