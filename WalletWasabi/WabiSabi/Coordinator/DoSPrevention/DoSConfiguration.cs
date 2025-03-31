namespace WalletWasabi.WabiSabi.Coordinator.DoSPrevention;

public record DoSConfiguration(
	decimal SeverityInBitcoinsPerHour,
	TimeSpan MinTimeForFailedToVerify,
	TimeSpan MinTimeForCheating,
	TimeSpan MinTimeInPrison,
	decimal PenaltyFactorForDisruptingConfirmation,
	decimal PenaltyFactorForDisruptingSignalReadyToSign,
	decimal PenaltyFactorForDisruptingSigning,
	decimal PenaltyFactorForDisruptingByDoubleSpending);
