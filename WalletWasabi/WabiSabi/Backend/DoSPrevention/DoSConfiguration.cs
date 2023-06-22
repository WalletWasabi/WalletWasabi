namespace WalletWasabi.WabiSabi.Backend.DoSPrevention;

public record DoSConfiguration(
	decimal Severity,
	TimeSpan MinTimeForFailedToVerify,
	TimeSpan MinTimeForCheating,
	TimeSpan MinimumTimeInPrison,
	decimal PenaltyFactorForDisruptingConfirmation,
	decimal PenaltyFactorForDisruptingSigning,
	decimal PenaltyFactorForDisruptingByDoubleSpending);
