using System;
using System.Collections.Generic;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields
{
	public class MethodsField : ByteArraySerializableBase
	{
		public MethodsField(byte[] bytes)
		{
			_ = Guard.NotNullOrEmpty(nameof(bytes), bytes);

			foreach (var b in bytes)
			{
				if (b != MethodField.NoAuthenticationRequired && b != MethodField.UsernamePassword)
				{
					throw new FormatException($"Unrecognized authentication method: {ByteHelpers.ToHex(b)}.");
				}
			}

			Bytes = bytes;
		}

		public MethodsField(params MethodField[] methods)
		{
			_ = Guard.NotNullOrEmpty(nameof(methods), methods);

			int count = methods.Length;
			Bytes = new byte[count];
			for (int i = 0; i < count; i++)
			{
				Bytes[i] = methods[i].ToByte();
			}
		}

		private byte[] Bytes { get; }

		public override byte[] ToBytes() => Bytes;
	}
}
