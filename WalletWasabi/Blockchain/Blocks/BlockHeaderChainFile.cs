using System.IO;

namespace WalletWasabi.Blockchain.Blocks;

/// <summary>
/// Persists a block header chain that starts at a checkpoint: 4 bytes base height followed by 80-byte headers.
/// </summary>
public static class BlockHeaderChainFile
{
	private const int HeaderSize = 80;

	public static byte[] Serialize(ConcurrentChain chain)
	{
		var headers = new List<BlockHeader>();
		for (var block = chain.Tip; block is not null; block = block.Previous)
		{
			headers.Add(block.Header);
		}

		headers.Reverse();
		var baseHeight = (uint)(chain.Tip!.Height - headers.Count + 1);

		using var ms = new MemoryStream(4 + headers.Count * HeaderSize);
		ms.Write(BitConverter.GetBytes(baseHeight));
		foreach (var header in headers)
		{
			ms.Write(header.ToBytes());
		}

		return ms.ToArray();
	}

	/// <summary>
	/// Loads the headers into <paramref name="chain"/>. The file must start with <paramref name="baseHeader"/> at
	/// <paramref name="baseHeight"/>; headers that do not continue the chain are dropped.
	/// </summary>
	public static bool TryLoad(byte[] bytes, Network network, uint baseHeight, BlockHeader baseHeader, ConcurrentChain chain)
	{
		if (bytes.Length < 4 + HeaderSize || (bytes.Length - 4) % HeaderSize != 0 || BitConverter.ToUInt32(bytes, 0) != baseHeight)
		{
			return false;
		}

		var count = (bytes.Length - 4) / HeaderSize;
		var tip = new ChainedBlock(baseHeader, (int)baseHeight);
		for (var i = 0; i < count; i++)
		{
			var header = network.Consensus.ConsensusFactory.CreateBlockHeader();
			header.ReadWrite(bytes.AsSpan(4 + i * HeaderSize, HeaderSize).ToArray(), network);
			var hash = header.GetHash();

			if (i == 0)
			{
				if (hash != baseHeader.GetHash())
				{
					return false;
				}

				continue;
			}

			if (header.HashPrevBlock != tip.HashBlock)
			{
				break;
			}

			tip = new ChainedBlock(header, hash, tip);
		}

		chain.SetTip(tip);
		return true;
	}
}
