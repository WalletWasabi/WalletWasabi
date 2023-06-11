using Newtonsoft.Json;
using WabiSabi.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models;

public record InputRegistrationResponse(
	Guid AliceId,
	CredentialsResponse AmountCredentials,
	CredentialsResponse VsizeCredentials,
	[property: JsonProperty("isPayingZeroCoordinationFee")] bool IsCoordinationFeeExempted
);
