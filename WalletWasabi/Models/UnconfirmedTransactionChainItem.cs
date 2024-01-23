using NBitcoin;

namespace WalletWasabi.Models;
public record UnconfirmedTransactionChainItem(uint256 TxId, int Size, Money Fee);
