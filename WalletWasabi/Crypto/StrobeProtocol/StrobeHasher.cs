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

		public void Append(string fieldName, object fieldValue)
			=> Append(fieldName, fieldValue switch
			{
				IBitcoinSerializable serializable => serializable.ToHex(),
				Money money => money.Satoshi.ToString(CultureInfo.InvariantCulture),
				string str => str,
				uint numUint => numUint.ToString(CultureInfo.InvariantCulture),
				ulong numUlong => numUlong.ToString(CultureInfo.InvariantCulture),
				decimal numDecimal => numDecimal.ToString(CultureInfo.InvariantCulture),
				CredentialIssuerParameters issuerParameters => issuerParameters.ToString(),
				_ => throw new ArgumentException($"{fieldValue.GetType().Name} doesn't have a string representation for strobe hasher.")
			});

		public void Append(string fieldName, string fieldValue)
		{
			strobe.AddAssociatedMetaData(Encoding.UTF8.GetBytes(fieldName), false);

			var serializedValue = Encoding.UTF8.GetBytes(fieldValue);
			strobe.AddAssociatedMetaData(BitConverter.GetBytes(serializedValue.Length), true);
			strobe.AddAssociatedData(serializedValue, false);
		}

		public uint256 GetHash()
		{
			return new uint256(strobe.Prf(32, false));
		}

		public static uint256 Combine(string domain, Dictionary<string, object> elements)
		{
			var hasher = new StrobeHasher(domain);
			foreach (var element in elements)
			{
				hasher.Append(element.Key, element.Value);
			}
			return hasher.GetHash();
		}
	}
}
