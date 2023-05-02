using WabiSabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabiClientLibrary.Models;

public record GetCredentialsResponse(
	Credential[] Credentials
);
