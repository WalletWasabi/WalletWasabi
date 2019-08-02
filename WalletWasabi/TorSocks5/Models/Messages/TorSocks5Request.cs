using System;
using System.Linq;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.TorSocks5.Models.Fields.OctetFields;
using WalletWasabi.TorSocks5.Models.TorSocks5.Fields.ByteArrayFields;
using WalletWasabi.TorSocks5.TorSocks5.Models.Fields.ByteArrayFields;

namespace WalletWasabi.TorSocks5.Models.Messages
{
	public class TorSocks5Request : ByteArraySerializableBase
	{
		#region PropertiesAndMembers

		public VerField Ver { get; set; }

		public CmdField Cmd { get; set; }

		public RsvField Rsv { get; set; }

		public AtypField Atyp { get; set; }

		public AddrField DstAddr { get; set; }

		public PortField DstPort { get; set; }

		#endregion PropertiesAndMembers

		#region ConstructorsAndInitializers

		public TorSocks5Request()
		{
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

		#endregion ConstructorsAndInitializers

		#region Serialization

		public override void FromBytes(byte[] bytes)
		{
			Guard.NotNullOrEmpty(nameof(bytes), bytes);
			Guard.MinimumAndNotNull($"{nameof(bytes)}.{nameof(bytes.Length)}", bytes.Length, 6);

			Ver = new VerField();
			Ver.FromByte(bytes[0]);

			Cmd = new CmdField();
			Cmd.FromByte(bytes[1]);

			Rsv = new RsvField();
			Rsv.FromByte(bytes[2]);

			Atyp = new AtypField();
			Atyp.FromByte(bytes[3]);

			DstAddr = new AddrField();
			DstAddr.FromBytes(bytes.Skip(4).Take(bytes.Length - 4 - 2).ToArray());

			DstPort = new PortField();
			DstPort.FromBytes(bytes.Skip(bytes.Length - 2).ToArray());
		}

		public override byte[] ToBytes() => ByteHelpers.Combine(new byte[] { Ver.ToByte(), Cmd.ToByte(), Rsv.ToByte(), Atyp.ToByte() }, DstAddr.ToBytes(), DstPort.ToBytes());

		#endregion Serialization
	}
}
