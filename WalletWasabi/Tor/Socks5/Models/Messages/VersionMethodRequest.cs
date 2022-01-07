using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.Tor.Socks5.Models.Bases;
using WalletWasabi.Tor.Socks5.Models.Fields.ByteArrayFields;
using WalletWasabi.Tor.Socks5.Models.Fields.OctetFields;

namespace WalletWasabi.Tor.Socks5.Models.Messages;

public class VersionMethodRequest : ByteArraySerializableBase
{
	#region Constructors

	public VersionMethodRequest(byte[] bytes)
	{
		Guard.NotNullOrEmpty(nameof(bytes), bytes);
		Guard.InRangeAndNotNull($"{nameof(bytes)}.{nameof(bytes.Length)}", bytes.Length, 3, 257);

		Ver = new VerField(bytes[0]);
		NMethods = new NMethodsField(bytes[1]);

		if (NMethods.Value != bytes.Length - 2)
		{
			throw new FormatException($"{nameof(NMethods)}.{nameof(NMethods.Value)} must be {nameof(bytes)}.{nameof(bytes.Length)} - 2 = {bytes.Length - 2}. Actual: {NMethods.Value}.");
		}

		Methods = new MethodsField(bytes.Skip(2).ToArray());
	}

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
