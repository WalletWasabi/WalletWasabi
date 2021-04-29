using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NBitcoin;
using WalletWasabi.WabiSabi;

namespace WalletWasabi.Crypto.StrobeProtocol
{
	public sealed class StrobeHasher
	{
		private Strobe128 strobe;

		private StrobeHasher(string domain)
		{
			strobe = new Strobe128(ProtocolConstants.WabiSabiProtocolIdentifier);
			Append(ProtocolConstants.DomainStrobeSeparator, domain);
		}
		public static StrobeHasher Create(string domain)
			=> new (domain);

		public StrobeHasher Append(string fieldName, IBitcoinSerializable serializable)
			=> Append(fieldName, serializable.ToBytes());

		public StrobeHasher Append(string fieldName, Money money)
			=> Append(fieldName, money.Satoshi);

		public StrobeHasher Append(string fieldName, uint num)
			=> Append(fieldName, BitConverter.GetBytes(num));

		public StrobeHasher Append(string fieldName, long num)
			=> Append(fieldName, BitConverter.GetBytes(num));

		public StrobeHasher Append(string fieldName, ulong num)
			=> Append(fieldName, BitConverter.GetBytes(num));

		public StrobeHasher Append(string fieldName, CredentialIssuerParameters issuerParameters)
			=> Append($"{fieldName}.Cw", issuerParameters.Cw.ToBytes())
			.Append($"{fieldName}.I", issuerParameters.I.ToBytes());

		public StrobeHasher Append(string fieldName, string str)
			=> Append($"{fieldName}.Cw", Encoding.UTF8.GetBytes(str));

		public StrobeHasher Append(string fieldName, byte[] serializedValue)
		{
			strobe.AddAssociatedMetaData(Encoding.UTF8.GetBytes(fieldName), false);
			strobe.AddAssociatedMetaData(BitConverter.GetBytes(serializedValue.Length), true);
			strobe.AddAssociatedData(serializedValue, false);
			return this;
		}

		public uint256 GetHash()
		{
			return new uint256(strobe.Prf(32, false));
		}
	}
}
