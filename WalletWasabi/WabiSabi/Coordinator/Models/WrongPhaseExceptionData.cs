using WalletWasabi.WabiSabi.Coordinator.Rounds;

namespace WalletWasabi.WabiSabi.Coordinator.Models;

public record WrongPhaseExceptionData(Phase CurrentPhase) : ExceptionData;
