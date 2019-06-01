using System;
using System.Collections.Generic;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.TorSocks5.Models.Fields.OctetFields;

namespace WalletWasabi.TorSocks5.Models.TorSocks5.Fields.ByteArrayFields
{
	public class MethodsField : ByteArraySerializableBase
	{
		#region PropertiesAndMembers

		private byte[] Bytes { get; set; }

		public IEnumerable<MethodField> Methods
		{
			get
			{
				foreach (var b in Bytes)
				{
					var method = new MethodField();
					method.FromByte(b);
					yield return method;
				}
			}
		}

		#endregion PropertiesAndMembers

		#region ConstructorsAndInitializers

		public MethodsField()
		{
		}

		public MethodsField(params MethodField[] methods)
		{
			Guard.NotNullOrEmpty(nameof(methods), methods);

			int count = methods.Length;
			Bytes = new byte[count];
			for (int i = 0; i < count; i++)
			{
				Bytes[i] = methods[i].ToByte();
			}
		}

		#endregion ConstructorsAndInitializers

		#region Serialization

		public override void FromBytes(byte[] bytes)
		{
			Guard.NotNullOrEmpty(nameof(bytes), bytes);

			foreach (var b in bytes)
			{
				if (b != MethodField.NoAuthenticationRequired && b != MethodField.UsernamePassword)
				{
					throw new FormatException($"Unrecognized authentication method: {ByteHelpers.ToHex(b)}.");
				}
			}

			Bytes = bytes;
		}

		public override byte[] ToBytes() => Bytes;

		#endregion Serialization
	}
}
