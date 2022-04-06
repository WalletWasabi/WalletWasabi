using NBitcoin;
using NBitcoin.Crypto;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Text;
using WalletWasabi.JsonConverters;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.JsonConverters;

/// <summary>
/// Tests for <see cref="CompactSignatureJsonConverter"/> class.
/// </summary>
public class CompactSignatureJsonConverterTests
{
	[Fact]
	public void SignatureTest()
	{
		// Get a compressed private key.
		string base58 = "Kwr371tjA9u2rFSMZjTNun2PXXP3WPZu2afRHTcta6KxEUdm1vEw";
		BitcoinSecret bitcoinSecret = Network.Main.CreateBitcoinSecret(base58);
		Key privateKey = bitcoinSecret.PrivateKey;
		Assert.True(privateKey.IsCompressed);

		uint256 hashMsg = Hashes.DoubleSHA256(Encoding.ASCII.GetBytes("compact hashing test"));
		byte[] compactSignature = privateKey.SignCompact(hashMsg);
		Assert.NotNull(compactSignature);

		string hex = ByteHelpers.ToHex(compactSignature);
		Assert.Equal("1F71932FFF735FA6A57787191A296717F71270B2B7E1D90008B7147117F250DBDE012359EED51D28682B1AAB686A8FD8A411A8D07F1EB4D7CDAC5B7EBE73F260A0", hex);
	}
}
