using System.Collections.Generic;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public record WalletVirtualInput(HdPubKey HdPubKey, HashSet<SmartCoin> SmartCoins)
{
}
