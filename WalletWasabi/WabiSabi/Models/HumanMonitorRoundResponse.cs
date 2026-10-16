using NBitcoin;

public record HumanMonitorRoundResponse(
	uint256 RoundId,
	bool IsBlameRound,
	int InputCount,
	TimeSpan InputRegistrationRemaining,
	string Phase);
