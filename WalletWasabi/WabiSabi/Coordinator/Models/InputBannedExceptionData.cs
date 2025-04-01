namespace WalletWasabi.WabiSabi.Coordinator.Models;

public record InputBannedExceptionData(DateTimeOffset BannedUntil) : ExceptionData;
