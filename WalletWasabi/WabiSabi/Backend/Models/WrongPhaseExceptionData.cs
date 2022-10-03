using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Backend.Models;

public record WrongPhaseExceptionData(Phase CurrentPhase) : ExceptionData;
