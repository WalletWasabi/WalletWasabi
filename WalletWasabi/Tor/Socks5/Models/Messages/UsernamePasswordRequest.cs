using System;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Models.Messages
{
	public class UsernamePasswordRequest : ByteArraySerializableBase
	{
		public UsernamePasswordRequest(byte[] bytes)
		{
			Guard.NotNullOrEmpty(nameof(bytes), bytes);
			Guard.InRangeAndNotNull($"{nameof(bytes)}.{nameof(bytes.Length)}", bytes.Length, 6, 513);

			Ver = new AuthVerField(bytes[0]);

			ULen = new ULenField();
			ULen.FromByte(bytes[1]);

			UName = new UNameField(bytes[2..(2 + ULen.Value)]);

			PLen = new PLenField();
			PLen.FromByte(bytes[1 + ULen.Value]);
			int expectedPlenValue = bytes.Length - 3 + ULen.Value;
			if (PLen.Value != expectedPlenValue)
			{
				throw new FormatException($"{nameof(PLen)}.{nameof(PLen.Value)} must be {nameof(bytes)}.{nameof(bytes.Length)} - 3 + {nameof(ULen)}.{nameof(ULen.Value)} = {expectedPlenValue}. Actual: {PLen.Value}.");
			}
			Passwd = new PasswdField(bytes[(3 + ULen.Value)..]);
		}

		public UsernamePasswordRequest(UNameField uName, PasswdField passwd)
		{
			Ver = AuthVerField.Version1;
			UName = Guard.NotNull(nameof(uName), uName);
			Passwd = Guard.NotNull(nameof(passwd), passwd);

			var pLen = new PLenField();
			var uLen = new ULenField();
			pLen.FromPasswdField(passwd);
			uLen.FromUNameField(uName);
			PLen = pLen;
			ULen = uLen;
		}

		public AuthVerField Ver { get; }

		public ULenField ULen { get; }

		public UNameField UName { get; }

		public PLenField PLen { get; }

		public PasswdField Passwd { get; }

		public override byte[] ToBytes() => ByteHelpers.Combine(new byte[] { Ver.ToByte(), ULen.ToByte() }, UName.ToBytes(), new byte[] { PLen.ToByte() }, Passwd.ToBytes());
	}
}
