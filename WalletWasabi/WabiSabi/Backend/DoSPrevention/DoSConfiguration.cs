namespace WalletWasabi.WabiSabi.Backend.DoSPrevention;

public record DoSConfiguration(
	decimal SeverityInBitcoinsPerHour,
	TimeSpan MinTimeForFailedToVerify,
	TimeSpan MinTimeForCheating,
	TimeSpan MinimumTimeInPrison,
	decimal PenaltyFactorForDisruptingConfirmation,
	decimal PenaltyFactorForDisruptingSigning,
	decimal PenaltyFactorForDisruptingByDoubleSpending);
