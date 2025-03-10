using System.Collections.Generic;
using NBitcoin;

namespace WalletWasabi.BitcoinRpc.Models;

public class VerboseBlockInfo
{
	public VerboseBlockInfo(uint256 prevBlockHash, ulong confirmations, uint256 hash, DateTimeOffset blockTime, ulong height, IEnumerable<VerboseTransactionInfo> transactions)
	{
		PrevBlockHash = prevBlockHash;
		Confirmations = confirmations;
		Hash = hash;
		BlockTime = blockTime;
		Height = height;
		Transactions = transactions;
	}

	public DateTimeOffset BlockTime { get; }

	public uint256 Hash { get; }

	public uint256 PrevBlockHash { get; }

	public ulong Confirmations { get; }

	public ulong Height { get; }

	public IEnumerable<VerboseTransactionInfo> Transactions { get; }
}
