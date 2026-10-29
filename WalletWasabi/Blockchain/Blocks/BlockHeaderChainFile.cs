namespace WalletWasabi.Blockchain.Blocks;

/// <summary>
/// Persists a block header chain that starts at a checkpoint: 4 bytes base height followed by 80-byte headers.
/// </summary>
public static class BlockHeaderChainFile
{
	private const int HeaderSize = 80;

	public static byte[] Serialize(ConcurrentChain chain)
	{
		var tip = chain.Tip!;
		var headers = tip.EnumerateToGenesis().Reverse().Select(b => b.Header.ToBytes()).ToArray();
		return [.. BitConverter.GetBytes((uint)(tip.Height - headers.Length + 1)), .. headers.SelectMany(x => x)];
	}

	/// <summary>
	/// Loads the headers into <paramref name="chain"/>. The file must start with <paramref name="checkpoint"/>;
	/// headers that do not continue the chain are dropped.
	/// </summary>
	public static bool TryLoad(byte[] bytes, Network network, BlockHeaderCheckpoint checkpoint, ConcurrentChain chain)
	{
		if (bytes.Length < 4 + HeaderSize
			|| (bytes.Length - 4) % HeaderSize != 0
			|| BitConverter.ToUInt32(bytes, 0) != checkpoint.Height
			|| !bytes.AsSpan(4, HeaderSize).SequenceEqual(checkpoint.Header.ToBytes()))
		{
			return false;
		}

		var tip = checkpoint.ToChainedBlock();
		for (var offset = 4 + HeaderSize; offset < bytes.Length; offset += HeaderSize)
		{
			var header = network.Consensus.ConsensusFactory.CreateBlockHeader();
			header.ReadWrite(bytes.AsSpan(offset, HeaderSize).ToArray(), network);
			if (header.HashPrevBlock != tip.HashBlock)
			{
				break;
			}

			tip = new ChainedBlock(header, header.GetHash(), tip);
		}

		chain.SetTip(tip);
		return true;
	}
}
