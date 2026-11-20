using NBitcoin;

namespace WalletWasabi.Blockchain.TransactionOutputs;

/// <summary>
/// Why a coinjoin round does not accept a coin.
/// </summary>
public abstract record CoinJoinIneligibility
{
	/// <summary>The round does not accept inputs of the coin's script type.</summary>
	public record ScriptTypeNotAllowed : CoinJoinIneligibility;

	/// <summary>The coin is worth less than the smallest input the round accepts.</summary>
	public record AmountTooLow(Money MinAllowedAmount) : CoinJoinIneligibility;

	/// <summary>The coin is worth more than the largest input the round accepts.</summary>
	public record AmountTooHigh(Money MaxAllowedAmount) : CoinJoinIneligibility;

	/// <summary>Spending the coin at the round's mining fee rate costs at least as much as the coin is worth.</summary>
	public record Uneconomical(FeeRate MiningFeeRate) : CoinJoinIneligibility;
}
