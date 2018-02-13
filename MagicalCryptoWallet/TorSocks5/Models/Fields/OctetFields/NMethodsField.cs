using MagicalCryptoWallet.Bases;
using System;
using MagicalCryptoWallet.TorSocks5.Models.TorSocks5.Fields.ByteArrayFields;
using DotNetEssentials;

namespace MagicalCryptoWallet.TorSocks5.Models.Fields.OctetFields
{
	public class NMethodsField : OctetSerializableBase
    {
		#region PropertiesAndMembers

		public int Value => ByteValue;

		#endregion

		#region ConstructorsAndInitializers

		public NMethodsField()
		{

		}

		public NMethodsField(int value)
		{
			ByteValue = (byte)Guard.InRangeAndNotNull(nameof(value), value, 0, 255);
		}

		#endregion

		#region Serialization
		
		public void FromMethodsField(MethodsField methods)
		{
			Guard.NotNull(nameof(methods), methods);

			ByteValue = (byte)methods.ToBytes().Length;
		}

		#endregion
	}
}
