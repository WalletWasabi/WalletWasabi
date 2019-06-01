using System;
using System.Linq;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.TorSocks5.Models.Fields.OctetFields;
using WalletWasabi.TorSocks5.TorSocks5.Models.Fields.ByteArrayFields;

namespace WalletWasabi.TorSocks5.Models.Messages
{
	public class UsernamePasswordRequest : ByteArraySerializableBase
	{
		#region PropertiesAndMembers

		public AuthVerField Ver { get; set; }

		public ULenField ULen { get; set; }

		public UNameField UName { get; set; }

		public PLenField PLen { get; set; }

		public PasswdField Passwd { get; set; }

		#endregion PropertiesAndMembers

		#region ConstructorsAndInitializers

		public UsernamePasswordRequest()
		{
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

		#endregion ConstructorsAndInitializers

		#region Serialization

		public override void FromBytes(byte[] bytes)
		{
			Guard.NotNullOrEmpty(nameof(bytes), bytes);
			Guard.InRangeAndNotNull($"{nameof(bytes)}.{nameof(bytes.Length)}", bytes.Length, 6, 513);

			Ver = new AuthVerField();
			Ver.FromByte(bytes[0]);

			ULen = new ULenField();
			ULen.FromByte(bytes[1]);

			UName = new UNameField();
			UName.FromBytes(bytes.Skip(2).Take(ULen.Value).ToArray());

			PLen = new PLenField();
			PLen.FromByte(bytes[1 + ULen.Value]);
			int expectedPlenValue = bytes.Length - 3 + ULen.Value;
			if (PLen.Value != expectedPlenValue)
			{
				throw new FormatException($"{nameof(PLen)}.{nameof(PLen.Value)} must be {nameof(bytes)}.{nameof(bytes.Length)} - 3 + {nameof(ULen)}.{nameof(ULen.Value)} = {expectedPlenValue}. Actual: {PLen.Value}.");
			}
			Passwd.FromBytes(bytes.Skip(3 + ULen.Value).ToArray());
		}

		public override byte[] ToBytes() => ByteHelpers.Combine(new byte[] { Ver.ToByte(), ULen.ToByte() }, UName.ToBytes(), new byte[] { PLen.ToByte() }, Passwd.ToBytes());

		#endregion Serialization
	}
}
