using System;
using NBitcoin;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;

namespace WalletWasabi.WabiSabi.Models
{
    public record ConnectionConfirmationRequest(
        uint256 RoundId,
        Guid AliceSecret,
        ZeroCredentialsRequest ZeroAmountCredentialRequests,
        RealCredentialsRequest RealAmountCredentialRequests,
        ZeroCredentialsRequest ZeroVsizeCredentialRequests,
        RealCredentialsRequest RealVsizeCredentialRequests
    );
}
