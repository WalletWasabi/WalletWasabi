using NBitcoin;

namespace WalletWasabi.WabiSabi.Models;

public record TransactionSignaturesRequest(uint256 RoundId, uint InputIndex, WitScript Witness);
