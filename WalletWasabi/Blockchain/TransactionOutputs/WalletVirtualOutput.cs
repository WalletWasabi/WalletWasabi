using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public record WalletVirtualOutput(byte[] KeyIdentifier, Money Amount, HashSet<OutPoint> Outpoints)
{
}
