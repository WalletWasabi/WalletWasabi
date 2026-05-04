using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.Keys;

public class WpkhWalletPolicyHelperTests
{
	[Fact]
	public void BasicTest()
	{
		var testNet = Network.TestNet;
		var encryptedSecret = new BitcoinEncryptedSecretNoEC(wif: "6PYJxoa2SLZdYADFyMp3wo41RKaKGNedC3vviix4VdjFfrt1LkKDmXmYTM", Network.Main);
		var chainCode = Convert.FromHexString("D9DAD5377AB84A44815403FF57B0ABC6825701560DAA0F0FCDDB5A52EBE12A6E");
		var accountPrivateKey = new ExtKey(encryptedSecret.GetKey(password: "123456"), chainCode);
		var keyPath = new KeyPath("84'/0'/0'");
		var masterFingerprint = new HDFingerprint(0x2fc4a4f3);

		var walletPolicies = WpkhWalletPolicyHelper.Get(testNet, masterFingerprint, accountPrivateKey, keyPath);

		var expected = "wpkh([f3a4c42f/84'/0'/0']tprv8ghYQhz7XQhoqDZG8SzbkqGCDTwAzyVVmUN3cUerPhUgK91Xvc4FaMJpYwrjuQ48WD7KdQ7Y6znKnaY9PXP8SiDLv1srjjs8NVYGuM7Hrrk/<0;1>/*)";
		var actual = walletPolicies.Private.FullDescriptor.ToString();
		Assert.Equal(expected, actual);

		expected = "wpkh([f3a4c42f/84'/0'/0']tpubDDPaZ82MfnPUigb426fCAEvJnVT7AJgQLmxptzh9oyH59dGJYzsqkqvgj6SyY9eBHhFmG286cfj66Dzv1kYAnC3o7LRxohvo7mwWPr26uje/<0;1>/*)";
		actual = walletPolicies.Public.FullDescriptor.ToString();
		Assert.Equal(expected, actual);
	}
}
