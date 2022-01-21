using NBitcoin;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WalletWasabi.WabiSabi.Models;

public record OutputRegistrationRequest(
	uint256 RoundId,
	[ValidateNever] Script Script,
	RealCredentialsRequest AmountCredentialRequests,
	RealCredentialsRequest VsizeCredentialRequests
);
