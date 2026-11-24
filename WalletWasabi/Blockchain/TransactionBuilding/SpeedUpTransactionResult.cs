using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Blockchain.TransactionBuilding;

/// <summary>
/// <paramref name="OriginalTransaction"/> was requested to speed up.
/// </summary>
public record SpeedUpTransactionResult(SmartTransaction OriginalTransaction, BuildTransactionResult BoostingTransaction, FeeRate OriginalFeeRate, FeeRate NewFeeRate);
