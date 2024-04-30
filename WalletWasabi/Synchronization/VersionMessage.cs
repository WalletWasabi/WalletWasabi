using System.IO;
using WalletWasabi.Extensions;

namespace WalletWasabi.Synchronization;

public record VersionMessage(Version ClientVersion, Version BackendVersion)
{
	public byte[] ToByteArray()
	{
		using var mem = new MemoryStream();
		using var writer = new BinaryWriter(mem);

		writer.Write((byte)ResponseMessage.SoftwareVersion);
		writer.Write(ClientVersion);
		writer.Write(BackendVersion);

		return mem.ToArray();
	}
}
