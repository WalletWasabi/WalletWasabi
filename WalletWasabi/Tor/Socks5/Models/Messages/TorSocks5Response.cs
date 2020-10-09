using System;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Models.Messages
{
	public class TorSocks5Response : ByteArraySerializableBase
	{
		#region Constructors

		public TorSocks5Response()
		{
		}

		public TorSocks5Response(RepField rep, AddrField bndAddr, PortField bndPort)
		{
			Rep = Guard.NotNull(nameof(rep), rep);
			BndAddr = Guard.NotNull(nameof(bndAddr), bndAddr);
			BndPort = Guard.NotNull(nameof(bndPort), bndPort);
			Ver = VerField.Socks5;
			Rsv = RsvField.X00;
			Atyp = bndAddr.Atyp;
		}

		#endregion Constructors

		#region PropertiesAndMembers

		public VerField Ver { get; set; }

		public RepField Rep { get; set; }

		public RsvField Rsv { get; set; }

		public AtypField Atyp { get; set; }

		public AddrField BndAddr { get; set; }

		public PortField BndPort { get; set; }

		#endregion PropertiesAndMembers

		#region Serialization

		public override void FromBytes(byte[] bytes)
		{
			Guard.NotNullOrEmpty(nameof(bytes), bytes);
			Guard.MinimumAndNotNull($"{nameof(bytes)}.{nameof(bytes.Length)}", bytes.Length, 6);

			Ver = new VerField(bytes[0]);

			Rep = new RepField();
			Rep.FromByte(bytes[1]);

			Rsv = new RsvField();
			Rsv.FromByte(bytes[2]);

			Atyp = new AtypField();
			Atyp.FromByte(bytes[3]);

			BndAddr = new AddrField();
			BndAddr.FromBytes(bytes[4..^2]);

			BndPort = new PortField();
			BndPort.FromBytes(bytes[^2..]);
		}

		public override byte[] ToBytes() => ByteHelpers.Combine(new byte[] { Ver.ToByte(), Rep.ToByte(), Rsv.ToByte(), Atyp.ToByte() }, BndAddr.ToBytes(), BndPort.ToBytes());

		#endregion Serialization
	}
}
