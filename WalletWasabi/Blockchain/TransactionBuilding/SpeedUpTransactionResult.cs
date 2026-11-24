using NBitcoin;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public record SpeedUpTransactionResult(BuildTransactionResult BoostingTransaction, FeeRate OriginalFeeRate, FeeRate NewFeeRate);
