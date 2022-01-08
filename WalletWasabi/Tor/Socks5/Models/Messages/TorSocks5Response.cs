using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Models.Messages;

public class TorSocks5Response : ByteArraySerializableBase
{
	public TorSocks5Response(byte[] bytes)
	{
		Guard.NotNullOrEmpty(nameof(bytes), bytes);
		Guard.MinimumAndNotNull($"{nameof(bytes)}.{nameof(bytes.Length)}", bytes.Length, smallest: 6);

		Ver = new VerField(bytes[0]);
		Rep = new RepField(bytes[1]);
		Rsv = new RsvField(bytes[2]);
		Atyp = new AtypField(bytes[3]);
		BndAddr = new AddrField(bytes[4..^2]);
		BndPort = new PortField(bytes[^2..]);
	}

	public VerField Ver { get; }

	public RepField Rep { get; }

	public RsvField Rsv { get; }

	public AtypField Atyp { get; }

	public AddrField BndAddr { get; }

	public PortField BndPort { get; }

	public override byte[] ToBytes() => ByteHelpers.Combine(new byte[] { Ver.ToByte(), Rep.ToByte(), Rsv.ToByte(), Atyp.ToByte() }, BndAddr.ToBytes(), BndPort.ToBytes());
}
