using System;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace WalletWasabi.WebClients.Wasabi;

public static class SupplementalCertificateTrust
{
	public static bool Validate(X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors errors, ReadOnlySpan<X509Certificate2> roots)
	{
		if (certificate is null) { return false; }
		if (errors == SslPolicyErrors.None) { return true; }
		// Supplemental public roots repair a missing trust anchor only. Name,
		// validity, usage, signatures and existing custom trust policies stay enforced.
		if (errors != SslPolicyErrors.RemoteCertificateChainErrors || chain is null || roots.IsEmpty
			|| chain.ChainPolicy.TrustMode != X509ChainTrustMode.System
			|| chain.ChainStatus.Length == 0
			|| Array.Exists(chain.ChainStatus, status => (status.Status & ~(X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.UntrustedRoot)) != 0))
		{ return false; }
		try
		{
			using var supplemented = new X509Chain();
			supplemented.ChainPolicy = chain.ChainPolicy.Clone();
			supplemented.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
			supplemented.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
			// AIA/CRL downloads must never create a connection outside Tor. Android
			// cannot enforce offline revocation; reject such a fallback instead.
			if (supplemented.ChainPolicy.RevocationMode != X509RevocationMode.NoCheck) { return false; }
			supplemented.ChainPolicy.DisableCertificateDownloads = true;
			supplemented.ChainPolicy.CustomTrustStore.Clear();
			foreach (var root in roots) { supplemented.ChainPolicy.CustomTrustStore.Add(root); }
			foreach (var element in chain.ChainElements) { supplemented.ChainPolicy.ExtraStore.Add(element.Certificate); }
			return supplemented.Build(certificate) && supplemented.ChainStatus.Length == 0;
		}
		catch (Exception ex) when (ex is CryptographicException or ArgumentException or PlatformNotSupportedException)
		{ return false; }
	}
}
