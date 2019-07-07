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

		public UsernameField Username { get; set; }

		public PLenField PLen { get; set; }

		public PasswordField Password { get; set; }

		#endregion PropertiesAndMembers

		#region ConstructorsAndInitializers

		public UsernamePasswordRequest()
		{
		}

		public UsernamePasswordRequest(UsernameField username, PasswordField password)
		{
			Ver = AuthVerField.Version1;
			Username = Guard.NotNull(nameof(username), username);
			Password = Guard.NotNull(nameof(password), password);

			var pLen = new PLenField();
			var uLen = new ULenField();
			pLen.FromPasswordField(password);
			uLen.FromUsernameField(username);
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

			Username = new UsernameField();
			Username.FromBytes(bytes.Skip(2).Take(ULen.Value).ToArray());

			PLen = new PLenField();
			PLen.FromByte(bytes[1 + ULen.Value]);
			int expectedPlenValue = bytes.Length - 3 + ULen.Value;
			if (PLen.Value != expectedPlenValue)
			{
				throw new FormatException($"{nameof(PLen)}.{nameof(PLen.Value)} must be {nameof(bytes)}.{nameof(bytes.Length)} - 3 + {nameof(ULen)}.{nameof(ULen.Value)} = {expectedPlenValue}. Actual: {PLen.Value}.");
			}
			Password.FromBytes(bytes.Skip(3 + ULen.Value).ToArray());
		}

		public override byte[] ToBytes() => ByteHelpers.Combine(new byte[] { Ver.ToByte(), ULen.ToByte() }, Username.ToBytes(), new byte[] { PLen.ToByte() }, Password.ToBytes());

		#endregion Serialization
	}
}
