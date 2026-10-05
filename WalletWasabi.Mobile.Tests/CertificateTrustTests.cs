using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Mobile.Tests;

public class CertificateTrustTests
{
	[Theory]
	[InlineData("valid", true)]
	[InlineData("wrong-host", false)]
	[InlineData("expired", false)]
	[InlineData("wrong-usage", false)]
	[InlineData("bad-signature", false)]
	[InlineData("unknown-root", false)]
	public void AdditionalRootPreservesCertificateProtections(string scenario, bool expected)
	{
		using var rootKey = RSA.Create(2048);
		var rootRequest = new CertificateRequest("CN=Synthetic test root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
		rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
		using var root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(10));
		using var leafKey = RSA.Create(2048);
		var leafRequest = new CertificateRequest("CN=localhost", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
		leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
		leafRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new(scenario == "wrong-usage" ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.1") }, true));
		using var issued = leafRequest.Create(root, DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(scenario == "expired" ? -1 : 1), RandomNumberGenerator.GetBytes(16));
		var bytes = issued.RawData;
		if (scenario == "bad-signature") { bytes[^1] ^= 1; }
		using var leaf = X509CertificateLoader.LoadCertificate(bytes);
		using var chain = new X509Chain();
		chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
		chain.ChainPolicy.DisableCertificateDownloads = true;
		chain.ChainPolicy.ExtraStore.Add(root);
		chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
		Assert.False(chain.Build(leaf));
		var errors = SslPolicyErrors.RemoteCertificateChainErrors;
		if (scenario == "wrong-host") { errors |= SslPolicyErrors.RemoteCertificateNameMismatch; }
		Assert.Equal(expected, SupplementalCertificateTrust.Validate(leaf, chain, errors, scenario == "unknown-root" ? [] : [root]));
	}

	[Fact]
	public void ExistingCustomTrustCannotBeOverridden()
	{
		using var key = RSA.Create(2048);
		var request = new CertificateRequest("CN=Synthetic untrusted root", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
		using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
		using var chain = new X509Chain();
		chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
		chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
		chain.ChainPolicy.DisableCertificateDownloads = true;
		Assert.False(chain.Build(certificate));
		Assert.False(SupplementalCertificateTrust.Validate(certificate, chain, SslPolicyErrors.RemoteCertificateChainErrors, [certificate]));
		Assert.False(SupplementalCertificateTrust.Validate(null, chain, SslPolicyErrors.RemoteCertificateNotAvailable, [certificate]));
	}
}
