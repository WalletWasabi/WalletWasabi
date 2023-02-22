using Newtonsoft.Json;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models;

public record InputRegistrationResponse(
	Guid AliceId,
	CredentialsResponse AmountCredentials,
	CredentialsResponse VsizeCredentials,
	[property: JsonProperty("isPayingZeroCoordinationFee")] bool IsCoordinationFeeExempted
);
