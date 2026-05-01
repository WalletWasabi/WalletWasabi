using NBitcoin;
using NBitcoin.WalletPolicies;
using WalletWasabi.Blockchain.Keys;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.Keys;

public class WpkhWalletPolicyHelperTests
{
	[Fact]
	public void BasicTest()
	{
		Network testNet = Network.TestNet;
		BitcoinEncryptedSecretNoEC encryptedSecret = new(wif: "6PYJxoa2SLZdYADFyMp3wo41RKaKGNedC3vviix4VdjFfrt1LkKDmXmYTM", Network.Main);
		byte[]? chainCode = Convert.FromHexString("D9DAD5377AB84A44815403FF57B0ABC6825701560DAA0F0FCDDB5A52EBE12A6E");
		ExtKey accountPrivateKey = new(encryptedSecret.GetKey(password: "123456"), chainCode);
		KeyPath keyPath = new("84'/0'/0'");
		HDFingerprint masterFingerprint = new(0x2fc4a4f3);

		WalletPolicy walletPolicy = WpkhWalletPolicyHelper.Get(testNet, masterFingerprint, accountPrivateKey, keyPath);

		string expected = "wpkh([f3a4c42f/84'/0'/0']tprv8ghYQhz7XQhoqDZG8SzbkqGCDTwAzyVVmUN3cUerPhUgK91Xvc4FaMJpYwrjuQ48WD7KdQ7Y6znKnaY9PXP8SiDLv1srjjs8NVYGuM7Hrrk/<0;1>/*)";
		Assert.Equal(expected, walletPolicy.FullDescriptor.ToString());
	}
}
