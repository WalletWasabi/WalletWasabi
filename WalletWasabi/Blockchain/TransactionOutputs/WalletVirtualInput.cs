using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Blockchain.TransactionOutputs;

public class WalletVirtualInput
{
	public WalletVirtualInput(byte[] id, ISet<SmartCoin> coins)
	{
		Id = id;
		Coins = coins;
	}

	public byte[] Id { get; }
	public ISet<SmartCoin> Coins { get; }
}
