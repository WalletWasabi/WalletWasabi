using System;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Models.Messages
{
	public class TorSocks5Request : ByteArraySerializableBase
	{
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

		public override byte[] ToBytes() => ByteHelpers.Combine(
			new byte[]
			{
				Ver.ToByte(), Cmd.ToByte(), Rsv.ToByte(), Atyp.ToByte()
			}
			, DstAddr.ToBytes()
			, DstPort.ToBytes());
	}
}
