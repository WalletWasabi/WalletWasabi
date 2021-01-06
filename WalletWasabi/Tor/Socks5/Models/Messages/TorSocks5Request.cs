using System;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Models.Messages
{
	/// <summary>
	/// SOCKS5 request representation.
	/// </summary>
	/// <remarks>
	/// <code>
	/// The SOCKS request is formed as follows:
	///  +----+-----+-------+------+----------+----------+
	///  |VER | CMD |  RSV  | ATYP | DST.ADDR | DST.PORT |
	///  +----+-----+-------+------+----------+----------+
	///  | 1  |  1  | X'00' |  1   | Variable |    2     |
	///  +----+-----+-------+------+----------+----------+
	/// </code>
	/// </remarks>
	/// <seealso href="https://tools.ietf.org/html/rfc1928">Section 4. Requests</seealso>
	public class TorSocks5Request : ByteArraySerializableBase
	{
		public TorSocks5Request(byte[] bytes)
		{
			Guard.NotNullOrEmpty(nameof(bytes), bytes);
			Guard.MinimumAndNotNull($"{nameof(bytes)}.{nameof(bytes.Length)}", bytes.Length, 6);

			Ver = new VerField(bytes[0]);
			Cmd = new CmdField(bytes[1]);
			Rsv = new RsvField(bytes[2]);
			Atyp = new AtypField(bytes[3]);
			DstAddr = new AddrField(bytes[4..^2]);
			DstPort = new PortField(bytes[^2..]);
		}

		public TorSocks5Request(CmdField cmd, AddrField dstAddr, PortField dstPort)
		{
			Cmd = Guard.NotNull(nameof(cmd), cmd);
			DstAddr = Guard.NotNull(nameof(dstAddr), dstAddr);
			DstPort = Guard.NotNull(nameof(dstPort), dstPort);
			Ver = VerField.Socks5;
			Rsv = RsvField.X00;
			Atyp = dstAddr.Atyp;
		}

		public VerField Ver { get; }

		public CmdField Cmd { get; }

		public RsvField Rsv { get; }

		public AtypField Atyp { get; }

		public AddrField DstAddr { get; }

		public PortField DstPort { get; }

		public override byte[] ToBytes() => ByteHelpers.Combine(new byte[] { Ver.ToByte(), Cmd.ToByte(), Rsv.ToByte(), Atyp.ToByte() }, DstAddr.ToBytes(), DstPort.ToBytes());
	}
}
