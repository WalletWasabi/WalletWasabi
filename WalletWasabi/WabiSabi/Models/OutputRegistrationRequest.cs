using NBitcoin;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using WabiSabi.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models;

public record OutputRegistrationRequest(
	uint256 RoundId,
	[ValidateNever] Script Script,
	RealCredentialsRequest AmountCredentialRequests,
	RealCredentialsRequest VsizeCredentialRequests
);
