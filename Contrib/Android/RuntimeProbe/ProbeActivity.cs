using Android.App;
using Android.OS;

namespace WalletWasabi.Android;

[Activity(Name = "io.wasabiwallet.android.RuntimeProbeActivity", MainLauncher = true, Exported = true)]
public sealed class ProbeActivity : Activity
{
  protected override void OnCreate(Bundle? savedInstanceState)
  {
    base.OnCreate(savedInstanceState);
    global::Android.Util.Log.Info("WasabiRuntime", "Probe activity started");
    global::Android.Util.Log.Info("WasabiRuntime", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + "; process " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
    _ = Task.Run(() =>
    {
      try
      {
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
  [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
  private static void InitializeCore() => System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(typeof(WalletWasabi.ModuleInitializer).Module.ModuleHandle);
}
