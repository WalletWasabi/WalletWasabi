using Android.OS;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Android;

internal static class AndroidCertificateTrust
{
  private static readonly Lazy<X509Certificate2> Root = new(() =>
  {
    using var resource = typeof(SupplementalCertificateTrust).Assembly.GetManifestResourceStream("WalletWasabi.Certificates.isrgrootx1.pem")
      ?? throw new InvalidOperationException("The supplemental public CA resource is missing.");
    using var reader = new StreamReader(resource);
    var certificate = X509Certificate2.CreateFromPem(reader.ReadToEnd());
    if (certificate.GetCertHashString(HashAlgorithmName.SHA256) != "96BCEC06264976F37460779ACF28C5A7CFE8A3C0AAE11A8FFCEE05C0BDDF08C6")
    { certificate.Dispose(); throw new CryptographicException("The supplemental public CA fingerprint is invalid."); }
    return certificate;
  });

  public static void Configure(HttpClientHandler handler)
  {
    if ((int)Build.VERSION.SdkInt > 25) { return; }
    handler.ServerCertificateCustomValidationCallback = (_, certificate, chain, errors) =>
      // The CA's published root-program retirement is earlier than the PEM's
      // notAfter. A future app update must reassess trust rather than extend it.
      DateTime.UtcNow < new DateTime(2030, 6, 4, 0, 0, 0, DateTimeKind.Utc)
        ? SupplementalCertificateTrust.Validate(certificate, chain, errors, [Root.Value])
        : errors == System.Net.Security.SslPolicyErrors.None;
  }
}
