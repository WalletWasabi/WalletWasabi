using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.Keys;

public class WpkhOutputDescriptorHelperTests
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

		WpkhOutputDescriptorHelper.WpkhDescriptors descriptors = WpkhOutputDescriptorHelper.GetOutputDescriptors(testNet, masterFingerprint, accountPrivateKey, keyPath);
		Assert.Equal("wpkh([f3a4c42f/84'/0'/0']tpubDDPaZ82MfnPUigb426fCAEvJnVT7AJgQLmxptzh9oyH59dGJYzsqkqvgj6SyY9eBHhFmG286cfj66Dzv1kYAnC3o7LRxohvo7mwWPr26uje/1/*)#z2666dqc", descriptors.PublicInternal.ToString());
		Assert.Equal("wpkh([f3a4c42f/84'/0'/0']tpubDDPaZ82MfnPUigb426fCAEvJnVT7AJgQLmxptzh9oyH59dGJYzsqkqvgj6SyY9eBHhFmG286cfj66Dzv1kYAnC3o7LRxohvo7mwWPr26uje/0/*)#n7lm8csq", descriptors.PublicExternal.ToString());
		Assert.Equal("wpkh([f3a4c42f/84'/0'/0']tprv8ghYQhz7XQhoqDZG8SzbkqGCDTwAzyVVmUN3cUerPhUgK91Xvc4FaMJpYwrjuQ48WD7KdQ7Y6znKnaY9PXP8SiDLv1srjjs8NVYGuM7Hrrk/1/*)#ktc4yfd7", descriptors.PrivateInternal);
		Assert.Equal("wpkh([f3a4c42f/84'/0'/0']tprv8ghYQhz7XQhoqDZG8SzbkqGCDTwAzyVVmUN3cUerPhUgK91Xvc4FaMJpYwrjuQ48WD7KdQ7Y6znKnaY9PXP8SiDLv1srjjs8NVYGuM7Hrrk/0/*)#8la5euax", descriptors.PrivateExternal);
	}
}
