using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto;

namespace WalletWasabi.WabiSabi.Backend.PostRequests
{
	public record ReadyToSignRequestRequest(
		uint256 RoundId,
		uint256 AliceId,
		OwnershipProof OwnershipProof);
}
