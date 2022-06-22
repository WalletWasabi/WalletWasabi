using NBitcoin;

public record HumanMonitorRoundResponse(
	uint256 RoundId,
	bool IsBlameRound,
	int InputCount,
	decimal MaxSuggestedAmount,
	TimeSpan InputRegistrationRemaining,
	string Phase);
