using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Models
{
	public record TransactionSignaturesRequest(uint256 RoundId, uint InputIndex, WitScript Witness);
}
