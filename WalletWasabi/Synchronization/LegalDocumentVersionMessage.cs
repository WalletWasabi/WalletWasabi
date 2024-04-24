using System.IO;
using WalletWasabi.Extensions;

namespace WalletWasabi.Synchronization;

public record LegalDocumentVersionMessage(Version Version)
{
	public byte[] ToByteArray()
	{
		using var mem = new MemoryStream();
		using var writer = new BinaryWriter(mem);

		writer.Write((byte) ResponseMessage.LegalDocumentVersion);
		writer.Write(Version);

		return mem.ToArray();
	}
}
