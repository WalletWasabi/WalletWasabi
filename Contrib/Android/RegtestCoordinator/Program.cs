using NBitcoin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator;

if (args.Length is < 1 or > 2 || args.Length == 2 && args[1] != "qualification") { throw new ArgumentException("Supply a private regtest fixture directory and optional qualification mode."); }
var qualification = args.Length == 2;
var dataDir = Path.GetFullPath(args[0]);
Directory.CreateDirectory(dataDir);
var config = new WabiSabiConfig(Path.Combine(dataDir, "Config.json"))
{
	Network = Network.RegTest,
	RegTestBitcoinRpcUri = "http://127.0.0.1:18543/",
	BitcoinRpcConnectionString = "wasabiandroid:wasabi-android-regtest",
	CoordinatorIdentifier = "WasabiAndroidRegtest",
	MaxInputCountByRound = 2,
	MinInputCountByRoundMultiplier = 1,
	StandardInputRegistrationTimeout = qualification ? TimeSpan.FromSeconds(100) : TimeSpan.FromMinutes(5),
	ConnectionConfirmationTimeout = TimeSpan.FromMinutes(2),
	// Managed credential proofs on an emulator share CPU with the host's
	// compiler and test runner. Avoid making host load a protocol failure.
	OutputRegistrationTimeout = qualification ? TimeSpan.FromSeconds(90) : TimeSpan.FromMinutes(3),
	TransactionSigningTimeout = TimeSpan.FromMinutes(2),
	PublishAsOnionService = false
};
config.ToFile();
using var host = WalletWasabi.Coordinator.Program.CreateHostBuilder(["--datadir=" + dataDir, "--urls=http://127.0.0.1:18545"])
	.ConfigureServices(services => services.AddSingleton<FeeRateProvider>(_ => _ =>
		Task.FromResult(new FeeRateEstimations(new Dictionary<int, FeeRate> { [2] = new(2m), [108] = new(1m) }))))
	.Build();
await host.RunWithTasksAsync();
