using System.Buffers.Binary;
using System.IO;

namespace WalletWasabi.Blockchain.Blocks;

/// <summary>
/// Serialization of the block header chain for the <c>BlockHeaders{Network}.dat</c> file.
/// </summary>
/// <remarks>
/// Format: <c>"WWBH"</c>, a version byte, the root height (uint32 LE), the header count (uint32 LE) and then the raw
/// 80-byte headers from the root upwards. The root may be a checkpoint (see <see cref="BlockHeaderCheckpoints"/>),
/// in which case the chain does not start at genesis. Files written by <see cref="ConcurrentChain.ToBytes"/>
/// (32-byte hash followed by the header, from genesis) are still accepted.
/// </remarks>
public static class BlockHeaderChainFile
{
	private const byte Version = 1;
	private const int HeaderSize = 80;
	private const int PrefixSize = 4 + 1 + 4 + 4;

	private static ReadOnlySpan<byte> Magic => "WWBH"u8;

	/// <summary>Creates a chain containing a single block: the checkpoint root at or below <paramref name="maxRootHeight"/>, or genesis when the network has no checkpoints.</summary>
	public static ConcurrentChain CreateRooted(Network network, uint maxRootHeight)
	{
		var root = BlockHeaderCheckpoints.TryGetRoot(network, maxRootHeight)
			?? new ChainedBlock(network.GetGenesis().Header, 0);

		var chain = new ConcurrentChain();
		chain.SetTip(root);
		return chain;
	}

	/// <summary>Height of the lowest block of the chain (0 for a chain starting at genesis).</summary>
	public static uint GetRootHeight(ConcurrentChain chain)
	{
		var block = chain.Tip ?? throw new InvalidOperationException("The chain has no tip.");
		while (block.Previous is { } previous)
		{
			block = previous;
		}

		return (uint)block.Height;
	}

	public static byte[] Serialize(ConcurrentChain chain)
	{
		var tip = chain.Tip ?? throw new InvalidOperationException("The chain has no tip.");
		var rootHeight = GetRootHeight(chain);
		var count = tip.Height - (int)rootHeight + 1;

		var bytes = new byte[PrefixSize + count * HeaderSize];
		var span = bytes.AsSpan();
		Magic.CopyTo(span);
		span[4] = Version;
		BinaryPrimitives.WriteUInt32LittleEndian(span[5..], rootHeight);
		BinaryPrimitives.WriteUInt32LittleEndian(span[9..], (uint)count);

		var block = tip;
		for (var i = count - 1; i >= 0; i--)
		{
			block!.Header.ToBytes().CopyTo(span.Slice(PrefixSize + i * HeaderSize, HeaderSize));
			block = block.Previous;
		}

		return bytes;
	}

	public static ConcurrentChain Deserialize(byte[] bytes, Network network)
	{
		if (!bytes.AsSpan().StartsWith(Magic))
		{
			// Legacy format written by NBitcoin, always rooted at genesis.
			return new ConcurrentChain(bytes, network);
		}

		if (bytes.Length < PrefixSize)
		{
			throw new InvalidDataException("Truncated block header chain file.");
		}

		if (bytes[4] != Version)
		{
			throw new InvalidDataException($"Unsupported block header chain file version {bytes[4]}.");
		}

		var rootHeight = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(5));
		var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(9));
		if (count == 0 || (long)bytes.Length != PrefixSize + (long)count * HeaderSize)
		{
			throw new InvalidDataException("Truncated block header chain file.");
		}

		ChainedBlock? block = null;
		for (var i = 0; i < count; i++)
		{
			var header = network.Consensus.ConsensusFactory.CreateBlockHeader();
			header.ReadWrite(bytes.AsSpan(PrefixSize + i * HeaderSize, HeaderSize).ToArray(), network);

			// The constructor verifies that the header links to the previous block.
			block = block is null
				? new ChainedBlock(header, (int)rootHeight)
				: new ChainedBlock(header, header.GetHash(), block);
		}

		var chain = new ConcurrentChain();
		chain.SetTip(block!);
		return chain;
	}
}
