using NBitcoin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator;

if (args.Length is < 1 or > 2 || args.Length == 2 && args[1] is not ("qualification" or "disruption" or "dropout-confirmation" or "restart-output")) { throw new ArgumentException("Supply a private regtest fixture directory and optional qualification/disruption mode."); }
var qualification = args.Length == 2;
var disruption = args.Length == 2 && args[1] is "disruption" or "dropout-confirmation";
var restart = args.Length == 2 && args[1] == "restart-output";
var dataDir = Path.GetFullPath(args[0]);
Directory.CreateDirectory(dataDir);
var config = new WabiSabiConfig(Path.Combine(dataDir, "Config.json"))
{
	Network = Network.RegTest,
	RegTestBitcoinRpcUri = "http://127.0.0.1:18543/",
	BitcoinRpcConnectionString = "wasabiandroid:wasabi-android-regtest",
	CoordinatorIdentifier = "WasabiAndroidRegtest",
	MaxInputCountByRound = disruption ? 3 : 2,
	MinInputCountByRoundMultiplier = disruption ? 2d / 3d : 1,
	MinInputCountByBlameRoundMultiplier = disruption ? 2d / 3d : 1,
	// Restart retries have independent randomized backoff. Keep one viable
	// round open long enough for both clients to join it; a 100-second window
	// created overlapping rounds every 40 seconds and split their registrations.
	StandardInputRegistrationTimeout = qualification && !restart ? TimeSpan.FromSeconds(100) : TimeSpan.FromMinutes(5),
	ConnectionConfirmationTimeout = TimeSpan.FromMinutes(2),
	// Managed credential proofs on an emulator share CPU with the host's
	// compiler and test runner. Avoid making host load a protocol failure.
	OutputRegistrationTimeout = qualification ? TimeSpan.FromSeconds(90) : TimeSpan.FromMinutes(3),
	TransactionSigningTimeout = TimeSpan.FromMinutes(2),
	PublishAsOnionService = false
};
config.ToFile();
using var host = WalletWasabi.Coordinator.Program.CreateHostBuilder(["--datadir=" + dataDir, "--urls=http://127.0.0.1:18545"])
	.ConfigureServices(services =>
	{
		services.AddSingleton<FeeRateProvider>(_ => _ => Task.FromResult(new FeeRateEstimations(new Dictionary<int, FeeRate> { [2] = new(2m), [108] = new(1m) })));
		if (args.Length == 2 && args[1] == "dropout-confirmation")
		{
			var participantDirectory = Path.Combine(Directory.GetParent(dataDir)!.FullName, "disrupted-participant-1");
			services.Configure<MvcOptions>(options => options.Filters.Add(new WithholdConfirmationFilter(participantDirectory)));
		}
	})
	.Build();
await host.RunWithTasksAsync();
