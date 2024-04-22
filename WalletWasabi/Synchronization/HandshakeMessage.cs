using System.IO;
using NBitcoin;
using WalletWasabi.Extensions;

namespace WalletWasabi.Synchronization;

public record HandshakeMessage(uint256 bestKnownBlockHash)
{
	public byte[] ToByteArray()
	{
		using var mem = new MemoryStream();
		using var writer = new BinaryWriter(mem);

		writer.Write((byte)RequestMessage.BestKnownBlockHash);
		writer.Write(bestKnownBlockHash);

		return mem.ToArray();
	}
}
