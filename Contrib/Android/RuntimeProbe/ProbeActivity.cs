using Android.App;
using Android.OS;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using WalletWasabi.WebClients.Wasabi;

[assembly: UsesPermission(global::Android.Manifest.Permission.Internet)]

namespace WalletWasabi.Android;

[Activity(Name = "io.wasabiwallet.android.RuntimeProbeActivity", MainLauncher = true, Exported = true)]
public sealed class ProbeActivity : Activity
{
  protected override void OnCreate(Bundle? savedInstanceState)
  {
    base.OnCreate(savedInstanceState);
    global::Android.Util.Log.Info("WasabiRuntime", "Probe activity started");
    global::Android.Util.Log.Info("WasabiRuntime", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + "; process " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
    var tlsOnly = Intent?.GetBooleanExtra("tls-local", false) is true;
    var javaTlsOnly = Intent?.GetBooleanExtra("tls-native-local", false) is true;
    var engineTlsOnly = Intent?.GetBooleanExtra("tls-engine-local", false) is true;
    var tlsVectors = Intent?.GetBooleanExtra("tls-vectors", false) is true;
    var fixtureCertificate = Intent?.GetStringExtra("fixture-certificate");
    var sqliteInfo = Intent?.GetBooleanExtra("sqlite-info", false) is true;
    _ = Task.Run(async () =>
    {
      try
      {
        if (tlsVectors)
        {
          await VerifyTlsVectorsAsync(fixtureCertificate ?? throw new ArgumentException("Supply the public fixture certificate."));
          return;
        }
        if (sqliteInfo)
        {
          SQLitePCL.Batteries_V2.Init();
          global::Android.Util.Log.Info("WasabiRuntime", "SQLite: " + SQLitePCL.raw.sqlite3_libversion().utf8_to_string());
          for (var i = 0; ; i++)
          {
            var option = SQLitePCL.raw.sqlite3_compileoption_get(i).utf8_to_string();
            if (option is null) { break; }
            global::Android.Util.Log.Info("WasabiRuntime", "SQLite option: " + option);
          }
          return;
        }
        if (engineTlsOnly)
        {
          await VerifyLocalJavaEngineRejectionAsync();
          return;
        }
        if (javaTlsOnly)
        {
          VerifyLocalJavaTlsRejection();
          return;
        }
        if (tlsOnly)
        {
          await VerifyLocalTlsRejectionAsync();
          return;
        }
        global::Android.Util.Log.Info("WasabiRuntime", "Initializing core assembly");
        InitializeCore();
        global::Android.Util.Log.Info("WasabiRuntime", "Core assembly initialized");
        RuntimeProbe.Verify();
        global::Android.Util.Log.Info("WasabiRuntime", "PASS: runtime probe");
      }
      catch (Exception ex)
      {
        global::Android.Util.Log.Error("WasabiRuntime", "FAIL: " + ex);
      }
    });
  }
  private static async Task VerifyTlsVectorsAsync(string publicCertificate)
  {
    using var certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(publicCertificate));
    // This trust anchor is confined to this isolated localhost fixture. Both
    // trust paths must validate the chain and reject a different hostname.
    async Task ConnectAsync(string host, bool trustFixture, bool expectAcceptance, bool supplementFixture = false)
    {
      using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
      using var tcp = new TcpClient();
      await tcp.ConnectAsync(System.Net.IPAddress.Loopback, 18446, timeout.Token);
      using var tls = supplementFixture
        ? new SslStream(tcp.GetStream(), false, (_, peer, chain, errors) => SupplementalCertificateTrust.Validate(peer as X509Certificate2, chain, errors, [certificate]))
        : new SslStream(tcp.GetStream());
      var options = new SslClientAuthenticationOptions { TargetHost = host, EnabledSslProtocols = SslProtocols.Tls12 };
      if (trustFixture)
      {
        options.CertificateChainPolicy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust };
        options.CertificateChainPolicy.CustomTrustStore.Add(certificate);
      }
      try
      {
        await tls.AuthenticateAsClientAsync(options, timeout.Token);
        if (!expectAcceptance) { throw new InvalidOperationException("An invalid fixture certificate or hostname was accepted."); }
        await tls.WriteAsync(System.Text.Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n"), timeout.Token);
        using var reader = new StreamReader(tls);
        var reply = await reader.ReadToEndAsync(timeout.Token);
        if (!reply.EndsWith("OK", StringComparison.Ordinal)) { throw new IOException("Authenticated fixture reply did not match."); }
        global::Android.Util.Log.Info("WasabiRuntime", "PASS: authenticated localhost TLS and response");
      }
      catch (AuthenticationException) when (!expectAcceptance)
      {
        global::Android.Util.Log.Info("WasabiRuntime", trustFixture || supplementFixture ? "PASS: wrong hostname rejected" : "PASS: untrusted certificate rejected");
      }
    }
    for (var i = 0; i < 3; i++)
    {
      await ConnectAsync("localhost", false, false);
      await ConnectAsync("different.invalid", true, false);
      await ConnectAsync("localhost", true, true);
      await ConnectAsync("different.invalid", false, false, true);
      await ConnectAsync("localhost", false, true, true);
    }
    global::Android.Util.Log.Info("WasabiRuntime", "PASS: TLS certificate and hostname vectors, three fresh cycles");
  }
  private static async Task VerifyLocalJavaEngineRejectionAsync()
  {
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    using var tcp = new TcpClient();
    await tcp.ConnectAsync(System.Net.IPAddress.Loopback, 18446, timeout.Token);
    using var context = Javax.Net.Ssl.SSLContext.Default!;
    using var engine = context.CreateSSLEngine("localhost", 18446)!;
    engine.UseClientMode = true;
    engine.SetEnabledProtocols(["TLSv1.2"]);
    using var parameters = engine.SSLParameters!;
    parameters.EndpointIdentificationAlgorithm = "HTTPS";
    engine.SSLParameters = parameters;
    using var input = Java.Nio.ByteBuffer.Allocate(65536)!;
    using var output = Java.Nio.ByteBuffer.Allocate(65536)!;
    using var application = Java.Nio.ByteBuffer.Allocate(65536)!;
    using var empty = Java.Nio.ByteBuffer.Allocate(0)!;
    var stream = tcp.GetStream();
    engine.BeginHandshake();
    try
    {
      for (var step = 0; step < 30; step++)
      {
        timeout.Token.ThrowIfCancellationRequested();
        var status = engine.HandshakeStatus!.ToString();
        global::Android.Util.Log.Info("WasabiRuntime", "Native SSLEngine: " + status);
        switch (status)
        {
          case "NEED_WRAP":
            output.Clear();
            using (var result = engine.Wrap(empty, output)!)
            {
              output.Flip();
              var bytes = new byte[output.Remaining()];
              output.Get(bytes);
              await stream.WriteAsync(bytes, timeout.Token);
            }
            break;
          case "NEED_UNWRAP":
            var incoming = new byte[16384];
            var count = await stream.ReadAsync(incoming, timeout.Token);
            if (count == 0) { throw new IOException("TLS fixture closed before certificate validation."); }
            input.Put(incoming, 0, count);
            input.Flip();
            using (var result = engine.Unwrap(input, application)!) { }
            input.Compact();
            break;
          case "NEED_TASK":
            while (engine.DelegatedTask is { } task) { using (task) { task.Run(); } }
            break;
          default:
            throw new InvalidOperationException("Native SSLEngine accepted the untrusted fixture or stopped unexpectedly.");
        }
      }
    }
    catch (Javax.Net.Ssl.SSLException)
    {
      global::Android.Util.Log.Info("WasabiRuntime", "PASS: native SSLEngine rejected the untrusted certificate");
      return;
    }
    throw new InvalidOperationException("Native SSLEngine exceeded its bounded handshake steps.");
  }
  private static void VerifyLocalJavaTlsRejection()
  {
    using var factory = Javax.Net.Ssl.SSLSocketFactory.Default!;
    using var socket = (Javax.Net.Ssl.SSLSocket)factory.CreateSocket("127.0.0.1", 18446)!;
    socket.SoTimeout = 15000;
    socket.SetEnabledProtocols(["TLSv1.2"]);
    using var parameters = socket.SSLParameters!;
    parameters.EndpointIdentificationAlgorithm = "HTTPS";
    socket.SSLParameters = parameters;
    global::Android.Util.Log.Info("WasabiRuntime", "Local native SSLSocket connected");
    try { socket.StartHandshake(); }
    catch (Javax.Net.Ssl.SSLException)
    {
      global::Android.Util.Log.Info("WasabiRuntime", "PASS: native SSLSocket rejected the untrusted certificate");
      return;
    }
    throw new InvalidOperationException("The native socket accepted the untrusted synthetic certificate");
  }
  // An isolated negative TLS reproduction: localhost only, with an untrusted
  // synthetic server certificate. It must fail authentication promptly, rather
  // than stalling in the platform SSLEngine. No wallet or Tor policy is changed.
  private static async Task VerifyLocalTlsRejectionAsync()
  {
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    using var tcp = new TcpClient();
    await tcp.ConnectAsync(System.Net.IPAddress.Loopback, 18446, timeout.Token);
    global::Android.Util.Log.Info("WasabiRuntime", "Local TLS TCP connected; core initialization skipped");
    using var tls = new SslStream(tcp.GetStream());
    try
    {
      await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
      {
        TargetHost = "localhost", EnabledSslProtocols = SslProtocols.Tls12
      }, timeout.Token);
    }
    catch (AuthenticationException)
    {
      global::Android.Util.Log.Info("WasabiRuntime", "PASS: local TLS rejected the untrusted certificate");
      return;
    }
    throw new InvalidOperationException("The untrusted synthetic certificate was accepted");
  }
  [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
  private static void InitializeCore() => System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(typeof(WalletWasabi.ModuleInitializer).Module.ModuleHandle);
}
