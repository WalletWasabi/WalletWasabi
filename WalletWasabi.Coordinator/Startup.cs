using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Cache;
using WalletWasabi.Discoverability;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.DoSPrevention;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.Userfacing;

[assembly: ApiController]

namespace WalletWasabi.Coordinator;

public class Startup(IConfiguration configuration)
{
	public IConfiguration Configuration { get; } = configuration;

	// This method gets called by the runtime. Use this method to add services to the container.
	public void ConfigureServices(IServiceCollection services)
	{
		string dataDir = Configuration["datadir"] ?? EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Coordinator"));
		Logger.InitializeDefaults(Path.Combine(dataDir, "Logs.txt"));

		services.AddMemoryCache();


		services.AddMvc(options => {
			options.InputFormatters.Insert(0, new WasabiJsonInputFormatter(Decode.CoordinatorMessageFromStreamAsync));
			options.InputFormatters.RemoveType<SystemTextJsonInputFormatter>();
			options.OutputFormatters.Insert(0, new WasabiJsonOutputFormatter(Encode.CoordinatorMessage));
			options.OutputFormatters.RemoveType<SystemTextJsonOutputFormatter>();
			options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(Script)));
		})
		.AddControllersAsServices();

		services.AddControllers();

		WabiSabiConfig config = WabiSabiConfig.LoadFile(Path.Combine(dataDir, "Config.json"));
		services.AddSingleton(config);

		services.AddSingleton<IdempotencyRequestCache>();
		services.AddSingleton<IRPCClient>(provider =>
		{
			string host = config.BitcoinCoreRpcEndPoint.ToString(config.Network.RPCPort);
			RPCClient rpcClient = new(
					authenticationString: config.BitcoinRpcConnectionString,
					hostOrUri: host,
					network: config.Network);

			IMemoryCache memoryCache = provider.GetRequiredService<IMemoryCache>();
			CachedRpcClient cachedRpc = new(rpcClient, memoryCache);
			return cachedRpc;
		});

		var network = config.Network;
		services.AddSingleton(_ => network);

		services.AddSingleton<Prison>(s => s.GetRequiredService<Warden>().Prison);
		services.AddSingleton<Warden>(s =>
			new Warden(
				Path.Combine(dataDir, "Prison.txt"),
				s.GetRequiredService<WabiSabiConfig>()));
		services.AddSingleton<CoinJoinFeeRateStatStore>(s =>
			CoinJoinFeeRateStatStore.LoadFromFile(
				Path.Combine(dataDir, "CoinJoinFeeRateStatStore.txt"),
				s.GetRequiredService<WabiSabiConfig>(),
				s.GetRequiredService<IRPCClient>()
				));
		services.AddSingleton<RoundParameterFactory>();
		services.AddBackgroundService<Arena>();

		services.AddSingleton<AnnouncerConfig>(_ => config.AnnouncerConfig);
		services.AddBackgroundService<CoordinatorAnnouncer>();

		services.AddSingleton<IdempotencyRequestCache>();
		services.AddStartupTask<StartupTask>();
		services.AddResponseCompression();
		services.AddRequestTimeouts(options =>
			options.DefaultPolicy =
				new RequestTimeoutPolicy
				{
					Timeout = TimeSpan.FromSeconds(5)
				});
	}

	[SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "This method gets called by the runtime. Use this method to configure the HTTP request pipeline")]
	public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
	{
		app.UseRouting();
		app.UseResponseCompression();
		app.UseEndpoints(endpoints => endpoints.MapControllers());
		app.UseRequestTimeouts();
	}
}
