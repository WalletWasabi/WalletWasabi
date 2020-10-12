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

		#region PropertiesAndMembers

		public VerField Ver { get; set; }

		public CmdField Cmd { get; set; }

		public RsvField Rsv { get; set; }

		public AtypField Atyp { get; set; }

		public AddrField DstAddr { get; set; }

		public PortField DstPort { get; set; }

		#endregion PropertiesAndMembers

		#region Serialization

		public override void FromBytes(byte[] bytes)
		{
			Guard.NotNullOrEmpty(nameof(bytes), bytes);
			Guard.MinimumAndNotNull($"{nameof(bytes)}.{nameof(bytes.Length)}", bytes.Length, 6);

			Ver = new VerField(bytes[0]);

			Cmd = new CmdField();
			Cmd.FromByte(bytes[1]);

			Rsv = new RsvField();
			Rsv.FromByte(bytes[2]);

			Atyp = new AtypField();
			Atyp.FromByte(bytes[3]);

			DstAddr = new AddrField();
			DstAddr.FromBytes(bytes[4..^2]);

			DstPort = new PortField();
			DstPort.FromBytes(bytes[^2..]);
		}

		public override byte[] ToBytes() => ByteHelpers.Combine(new byte[] { Ver.ToByte(), Cmd.ToByte(), Rsv.ToByte(), Atyp.ToByte() }, DstAddr.ToBytes(), DstPort.ToBytes());

		#endregion Serialization
	}
}
