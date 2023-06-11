using NBitcoin;
using WabiSabi.CredentialRequesting;
using WalletWasabi.Crypto;

namespace WalletWasabi.WabiSabi.Models;

public record InputRegistrationRequest(
	uint256 RoundId,
	OutPoint Input,
	OwnershipProof OwnershipProof,
	ZeroCredentialsRequest ZeroAmountCredentialRequests,
	ZeroCredentialsRequest ZeroVsizeCredentialRequests
);
