using Newtonsoft.Json;

namespace WalletWasabi.WabiSabi.Backend.Models;

public record InputBannedExceptionData(DateTimeOffset BannedUntil) : ExceptionData;
