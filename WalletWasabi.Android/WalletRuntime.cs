using Android.Content;
using WalletWasabi.Logging;
using WalletWasabi.Mobile;

namespace WalletWasabi.Android;

internal static class WalletRuntime
{
  private static readonly SemaphoreSlim Gate = new(1, 1);
  private static TorHost? _tor;
  private static CancellationTokenSource? _stop;
  private static Task? _monitor;
  private static bool _loggingConfigured;
  private static RuntimeLifecycle _lifecycle = RuntimeLifecycle.Stopped;
  private static RuntimeStatus _snapshot = new(RuntimeLifecycle.Stopped, 0, 0, false, "Idle", 0, null);
  public static WalletSession? Session { get; private set; }
  public static string? Error { get; private set; }
  public static bool InterfaceForeground { get; set; }
  public static int Bootstrap => _tor?.Bootstrap ?? 0;
  public static RuntimeStatus Snapshot => Volatile.Read(ref _snapshot);
  private static void PublishStatus() => Volatile.Write(ref _snapshot, new(_lifecycle, Bootstrap, Session?.Global.GetPeerCount() ?? 0,
    Session?.IsSynchronized is true, Session?.CoinJoinStatus ?? "Idle",
    Session?.PendingTransactions.Count(e => e.State is SubmissionState.Pending or SubmissionState.Uncertain) ?? 0, Error));
  public static string DataDir(Context context) => Path.Combine(context.FilesDir!.AbsolutePath, "Wasabi");

  public static async Task StartAsync(Context context)
  {
    await Gate.WaitAsync().ConfigureAwait(false);
    try
    {
      if (Session is not null || _stop is not null) { return; }
      Error = null;
      _lifecycle = RuntimeLifecycle.Starting;
      PublishStatus();
      _stop = new();
      var dataDir = DataDir(context);
      Directory.CreateDirectory(dataDir);
      if (!_loggingConfigured)
      {
        Logger.Configure(Path.Combine(dataDir, "Logs.txt"), LogLevel.Warning, [LogMode.File]);
        _loggingConfigured = true;
      }
      await StartCoreAsync(context.ApplicationContext!, _stop.Token).ConfigureAwait(false);
      var token = _stop.Token;
      _monitor = Task.Run(() => MonitorAsync(context.ApplicationContext!, token));
    }
    catch (Exception)
    {
      Error = "Wallet startup failed. Reopen Wasabi to retry.";
      _lifecycle = RuntimeLifecycle.Failed;
      try { await DisposeCoreAsync().ConfigureAwait(false); }
      finally { _stop?.Dispose(); _stop = null; }
    }
    finally { PublishStatus(); Gate.Release(); }
  }

  private static async Task StartCoreAsync(Context context, CancellationToken cancellationToken)
  {
    var dataDir = DataDir(context);
    var settings = ReadSettings(context);
    settings = settings with { BitcoinRpcCredentials = new CredentialVault(context, dataDir).RetrieveRpcCredentials() };
    _tor = new();
    await _tor.StartAsync(context, dataDir, settings, cancellationToken).ConfigureAwait(false);
    var session = new WalletSession(dataDir, settings, context.ApplicationInfo!.NativeLibraryDir!,
      AppIdentity.IsPersonal ? WalletPolicy.Personal : WalletPolicy.Development, AppIdentity.SocksPort,
      () => _tor is { IsAlive: true, Bootstrap: 100 });
    Session = session;
    await session.InitializeAsync(cancellationToken).ConfigureAwait(false);
    _lifecycle = RuntimeLifecycle.Ready;
    PublishStatus();
  }

  public static MobileSettings ReadSettings(Context context) => MobileSettings.Load(DataDir(context), InitialSettings(context));

  private static MobileSettings InitialSettings(Context context)
  {
    var defaults = new MobileSettings { Network = AppIdentity.IsPersonal ? "main" : "testnet" };
    if (!AppIdentity.IsPersonal) { return defaults; }
    try
    {
      using var reader = new StreamReader(context.Assets!.Open("PersonalCoordinator.json"));
      using var document = System.Text.Json.JsonDocument.Parse(reader.ReadToEnd());
      // The bootstrap accepts exactly the two approved coordinator fields.
      if (document.RootElement.EnumerateObject().Any(p => p.Name is not "Coordinator" and not "CoordinatorIdentifier")) { throw new FormatException("Invalid coordinator bootstrap."); }
      return defaults with
      {
        Coordinator = document.RootElement.GetProperty("Coordinator").GetString()!,
        CoordinatorIdentifier = document.RootElement.GetProperty("CoordinatorIdentifier").GetString()!
      };
    }
    catch (Java.IO.FileNotFoundException) { return defaults; }
  }

  private static async Task MonitorAsync(Context context, CancellationToken cancellationToken)
  {
    try
    {
      while (!cancellationToken.IsCancellationRequested)
      {
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
          if (_tor is not { IsAlive: true })
          {
            _lifecycle = RuntimeLifecycle.Reconnecting;
            PublishStatus();
            await DisposeCoreAsync().ConfigureAwait(false);
            await StartCoreAsync(context, cancellationToken).ConfigureAwait(false);
          }
          if (Session is { } session)
          {
            _lifecycle = session.Global.GetPeerCount() == 0 ? RuntimeLifecycle.Reconnecting : RuntimeLifecycle.Ready;
            await session.ReconcilePendingAsync(cancellationToken).ConfigureAwait(false);
          }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
          Error = "Private connection interrupted. Reopen Wasabi to reconnect.";
          _lifecycle = RuntimeLifecycle.Failed;
          try { await DisposeCoreAsync().ConfigureAwait(false); }
          finally { _stop?.Dispose(); _stop = null; }
          return;
        }
        finally { PublishStatus(); Gate.Release(); }
      }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
  }

  private static async Task DisposeCoreAsync()
  {
    var session = Session;
    Session = null;
    try { if (session is not null) { await session.DisposeAsync().ConfigureAwait(false); } }
    finally
    {
      var tor = _tor;
      _tor = null;
      if (tor is not null) { await tor.DisposeAsync().ConfigureAwait(false); }
    }
  }

  public static async Task StopAsync()
  {
    // Cancellation must interrupt startup before waiting for its gate. The
    // monitor can retire this source concurrently after a failed connection.
    try { Volatile.Read(ref _stop)?.Cancel(); }
    catch (ObjectDisposedException) { }
    await Gate.WaitAsync().ConfigureAwait(false);
    Task? monitor;
    try
    {
      _lifecycle = RuntimeLifecycle.Stopping;
      PublishStatus();
      monitor = _monitor;
      _monitor = null;
      try { await DisposeCoreAsync().ConfigureAwait(false); }
      finally
      {
        _stop?.Dispose(); _stop = null; Error = null;
        _lifecycle = RuntimeLifecycle.Stopped;
      }
    }
    finally { PublishStatus(); Gate.Release(); }
    if (monitor is not null) { await monitor.ConfigureAwait(false); }
  }
}
