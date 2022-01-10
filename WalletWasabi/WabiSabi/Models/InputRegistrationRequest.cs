using NBitcoin;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models;

public record InputRegistrationRequest(
	uint256 RoundId,
	OutPoint Input,
	OwnershipProof OwnershipProof,
	ZeroCredentialsRequest ZeroAmountCredentialRequests,
	ZeroCredentialsRequest ZeroVsizeCredentialRequests
);
