using System;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using NBitcoin;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
	public record OutputRegistrationRequest(
		uint256 RoundId,
		[ValidateNever] Script Script,
		RealCredentialsRequest AmountCredentialRequests,
		RealCredentialsRequest VsizeCredentialRequests
	);
}
