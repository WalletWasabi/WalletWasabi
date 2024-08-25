using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Models.Messages;

public class UsernamePasswordRequest : ByteArraySerializableBase
{
	public UsernamePasswordRequest(UNameField uName, PasswdField passwd)
	{
		Ver = AuthVerField.Version1;
		UName = Guard.NotNull(nameof(uName), uName);
		Passwd = Guard.NotNull(nameof(passwd), passwd);
		PLen = new PLenField(passwd);
		ULen = new ULenField(uName);
	}

	public AuthVerField Ver { get; }

	public ULenField ULen { get; }

	public UNameField UName { get; }

	public PLenField PLen { get; }

	public PasswdField Passwd { get; }

	public override byte[] ToBytes() => ByteHelpers.Combine(new byte[] { Ver.ToByte(), ULen.ToByte() }, UName.ToBytes(), new byte[] { PLen.ToByte() }, Passwd.ToBytes());
}
