using WalletWasabi.WabiSabi.Backend.Models;

namespace WalletWasabi.WabiSabi.Models;

public record Error(
	string Type,
	string ErrorCode,
	string Description,
	ExceptionData ExceptionData
);
