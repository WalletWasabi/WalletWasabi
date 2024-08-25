using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Models.Messages;

public class VersionMethodRequest : ByteArraySerializableBase
{
	#region Constructors

	public VersionMethodRequest(MethodsField methods)
	{
		Methods = Guard.NotNull(nameof(methods), methods);

		Ver = VerField.Socks5;

		// The NMETHODS field contains the number of method identifier octets that appear in the METHODS field.
		NMethods = new NMethodsField(methods);
	}

	#endregion Constructors

	#region PropertiesAndMembers

	public VerField Ver { get; }

	public NMethodsField NMethods { get; }

	public MethodsField Methods { get; }

	#endregion PropertiesAndMembers

	#region Serialization

	public override byte[] ToBytes() => ByteHelpers.Combine(
		new byte[]
		{
				Ver.ToByte(),
				NMethods.ToByte()
		},
		Methods.ToBytes());

	#endregion Serialization
}
