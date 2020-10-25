using System.Net;
using System.Net.Sockets;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;

namespace WalletWasabi.Tor.Socks5.Models.Fields.OctetFields
{
	public class AtypField : OctetSerializableBase
	{
		public AtypField(byte value)
		{
			ByteValue = value;
		}

		public AtypField(string dstAddr)
		{
			dstAddr = Guard.NotNullOrEmptyOrWhitespace(nameof(dstAddr), dstAddr, true);

			if (IPAddress.TryParse(dstAddr, out IPAddress address))
			{
				Guard.Same($"{nameof(address)}.{nameof(address.AddressFamily)}", AddressFamily.InterNetwork, address.AddressFamily);

				ByteValue = IPv4.ToByte();
			}
			else
			{
				ByteValue = DomainName.ToByte();
			}
		}

		// https://gitweb.torproject.org/torspec.git/tree/socks-extensions.txt
		// IPv6 is not supported in CONNECT commands.

		public static readonly AtypField IPv4 = new AtypField(0x01);

		public static AtypField DomainName = new AtypField(0x03);
	}
}
