using Newtonsoft.Json;

namespace WalletWasabi.WabiSabi.Backend.Models;

public record InputBannedExceptionData([JsonProperty(PropertyName = "BannedUntil")] DateTimeOffset BannedUntil) : ExceptionData;
