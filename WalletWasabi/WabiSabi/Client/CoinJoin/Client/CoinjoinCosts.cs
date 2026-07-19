using NBitcoin;

namespace WalletWasabi.WabiSabi.Client;

/// <summary>
/// What a coinjoin transaction cost to the wallet: the share of the mining fee paid for the wallet's
/// inputs and outputs, the leftover amount that couldn't be decomposed into outputs (wasted dust) and
/// the total value of the payments made within the coinjoin.
/// </summary>
public record CoinjoinCosts(uint256 TransactionId, Money MiningFee, Money WastedDust, Money PaymentsTotal);
