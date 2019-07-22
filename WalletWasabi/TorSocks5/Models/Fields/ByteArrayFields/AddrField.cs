using System;
using System.Linq;
using System.Text;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.TorSocks5.Models.Fields.OctetFields;

namespace WalletWasabi.TorSocks5.Models.TorSocks5.Fields.ByteArrayFields
{
	public class AddrField : ByteArraySerializableBase
	{
		#region PropertiesAndMembers

		private byte[] Bytes { get; set; }

		public AtypField Atyp { get; set; }

		public string DomainOrIPv4
		{
			get
			{
				if (Atyp == AtypField.DomainName)
				{
					return Encoding.ASCII.GetString(Bytes.Skip(1).ToArray()); // UTF8 result in general SOCKS server failure
				}

				if (Atyp != AtypField.IPv4)
				{
					throw new NotSupportedException($"{nameof(Atyp)} is not supported. Value: {Atyp}.");
				}

				var values = new string[4];
				for (int i = 0; i < 4; i++)
				{
					values[i] = Bytes[i].ToString(); // it's ok ASCII here, these are always numbers
				}
				return string.Join(".", values);
			}
		}

		#endregion PropertiesAndMembers

		#region ConstructorsAndInitializers

		public AddrField()
		{
		}

		/// <param name="dstAddr">domain or IPv4</param>
		public AddrField(string dstAddr)
		{
			dstAddr = Guard.NotNullOrEmptyOrWhitespace(nameof(dstAddr), dstAddr, true);

			var atyp = new AtypField();
			atyp.FromDstAddr(dstAddr);

			Atyp = atyp;

			byte[] bytes;
			if (atyp == AtypField.DomainName)
			{
				// https://www.ietf.org/rfc/rfc1928.txt
				// the address field contains a fully-qualified domain name.  The first
				// octet of the address field contains the number of octets of name that
				// follow, there is no terminating NUL octet.
				var domainBytes = Encoding.ASCII.GetBytes(dstAddr); // Tor only knows ASCII, UTF8 results in general SOCKS server failure
				var numberOfOctets = domainBytes.Length;
				if (numberOfOctets > 255)
				{
					throw new FormatException($"{nameof(dstAddr)} can be maximum 255 octets. Actual: {numberOfOctets} octets. Value: {dstAddr}.");
				}

				bytes = ByteHelpers.Combine(new byte[] { (byte)numberOfOctets }, domainBytes);
			}
			else if (atyp == AtypField.IPv4)
			{
				// the address is a version-4 IP address, with a length of 4 octets
				var parts = dstAddr.Split(".", StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length != 4 || parts.Any(string.IsNullOrWhiteSpace))
				{
					throw new FormatException($"{nameof(dstAddr)} must be 4 parts. Actual: {parts.Length} parts. Value: {dstAddr}.");
				}

				bytes = new byte[4];
				for (int i = 0; i < 4; i++)
				{
					if (int.TryParse(parts[i], out int partInt))
					{
						if (partInt < 0 || partInt > 255)
						{
							throw new FormatException($"`Every part of {nameof(dstAddr)} must be between 0 and 255. The {i}. part is invalid: {partInt}. Value of {nameof(dstAddr)}: {dstAddr}");
						}
						bytes[i] = (byte)partInt;
					}
					else
					{
						throw new FormatException($"Could not parse the {i}. part of {nameof(dstAddr)} to int. Invalid part: {partInt}. Value of {nameof(dstAddr)}: {dstAddr}.");
					}
				}
			}
			else
			{
				throw new NotSupportedException($"{nameof(atyp)} is not supported. Value: {atyp}.");
			}

			Bytes = bytes;
		}

		#endregion ConstructorsAndInitializers

		#region Serialization

		public override void FromBytes(byte[] bytes)
		{
			Bytes = Guard.NotNullOrEmpty(nameof(bytes), bytes);

			AtypField atyp;
			if (bytes.First() == bytes.Length - 1 && bytes.Length != 4)
			{
				atyp = AtypField.DomainName;
			}
			else if (bytes.Length == 4)
			{
				atyp = AtypField.IPv4;
			}
			else
			{
				throw new FormatException($"Could not read IPv4 or domain name from {nameof(bytes)}. Value: {bytes}.");
			}

			Atyp = atyp;
		}

		public override byte[] ToBytes() => Bytes;

		public override string ToString() => DomainOrIPv4;

		#endregion Serialization
	}
}
