using System.IO;
using WalletWasabi.Backend.Models;
using WalletWasabi.Extensions;

namespace WalletWasabi.Synchronization;

public enum RequestMessage
{
	BestKnownBlockHash
}

public enum ResponseMessage
{
	Filter,
	HandshakeError,
	BlockHeight,
	ExchangeRate,
	MiningFeeRates
}

public record FilterMessage(FilterModel filterModel)
{
	public byte[] ToByteArray()
	{
		using var mem = new MemoryStream();
		using var writer = new BinaryWriter(mem);

		writer.Write((byte)ResponseMessage.Filter);
		writer.Write(filterModel);

		return mem.ToArray();
	}
}
