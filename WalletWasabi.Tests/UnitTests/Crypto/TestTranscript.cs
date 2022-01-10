using NBitcoin.Secp256k1;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.StrobeProtocol;
using WalletWasabi.WabiSabi;

namespace WalletWasabi.Tests.UnitTests.Crypto;

public class TestTranscript
{
	private Strobe128 _strobe;

	public TestTranscript(byte[] label)
	{
		_strobe = new Strobe128(ProtocolConstants.WabiSabiProtocolIdentifier);

		var metadata = Enumerable.Concat(
			Encoding.UTF8.GetBytes(ProtocolConstants.DomainStrobeSeparator),
			BitConverter.GetBytes(label.Length)).ToArray();
		_strobe.AddAssociatedMetaData(metadata, false);
		_strobe.AddAssociatedData(label, false);
	}

	public void CommitPublicNonces(IEnumerable<GroupElement> nonces)
	{
		var nonceArray = nonces.ToArray();

		var label = Encoding.UTF8.GetBytes("nonce-commitment");
		var metadata = Enumerable.Concat(label, BitConverter.GetBytes(nonceArray.Length)).ToArray();
		_strobe.AddAssociatedMetaData(metadata, false);

		for (var i = 0; i < nonceArray.Length; i++)
		{
			metadata = Enumerable.Concat(BitConverter.GetBytes(i), BitConverter.GetBytes(33)).ToArray();
			_strobe.AddAssociatedMetaData(metadata, false);
			_strobe.AddAssociatedData(nonceArray[i].ToBytes(), false);
		}
	}

	public Scalar GenerateChallenge()
	{
		_strobe.AddAssociatedMetaData(Encoding.UTF8.GetBytes("challenge"), false);
		return new Scalar(_strobe.Prf(32, false));
	}
}
