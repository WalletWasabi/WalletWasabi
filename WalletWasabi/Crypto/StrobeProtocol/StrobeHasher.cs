using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using NBitcoin;

namespace WalletWasabi.Crypto.StrobeProtocol
{
	public sealed class StrobeHasher
	{
		private Strobe128 strobe;

		private StrobeHasher()
		{
			strobe = new Strobe128("WabiSabi_v1.0");
			Append("domain-separator", "round-parameters");
		}


		public void Append<T>(string fieldName, T fieldValue) where T : notnull
		{
			strobe.AddAssociatedMetaData(Encoding.UTF8.GetBytes(fieldName), false);

			var serializedValue = Encoding.UTF8.GetBytes($"{fieldValue}");
			strobe.AddAssociatedMetaData(BitConverter.GetBytes(serializedValue.Length), true);
			strobe.AddAssociatedData(serializedValue, false);
		}

		public uint256 GetHash()
		{
			return new uint256(strobe.Prf(32, false));
		}

		public static uint256 Combine(Dictionary<string, object> elements)
		{
			var hasher = new StrobeHasher(); 
			foreach(var element in elements)
			{
				hasher.Append(element.Key, element.Value);
			}
			return hasher.GetHash();
		}
	}
}
