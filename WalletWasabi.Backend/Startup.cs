using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin.RPC;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc.Formatters;
using WalletWasabi.Backend.Middlewares;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Cache;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;
using WalletWasabi.Userfacing;
using WalletWasabi.WebClients;

[assembly: ApiController]

namespace WalletWasabi.Backend;

public class Startup
{
	public Startup(IConfiguration configuration)
	{
		Configuration = configuration;
	}

	public IConfiguration Configuration { get; }

	// This method gets called by the runtime. Use this method to add services to the container.
	public void ConfigureServices(IServiceCollection services)
	{
		string dataDir = Configuration["datadir"] ?? EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Backend"));
		Logger.InitializeDefaults(Path.Combine(dataDir, "Logs.txt"));

		services.AddMemoryCache();
		services.AddMvc(options =>
			{
				options.OutputFormatters.Insert(0, new WasabiJsonOutputFormatter(Encode.BackendMessage));
				options.InputFormatters.RemoveType<SystemTextJsonInputFormatter>();
				options.OutputFormatters.RemoveType<SystemTextJsonOutputFormatter>();
			})
			.AddControllersAsServices();

		services.AddControllers();

		services.AddSingleton<IExchangeRateProvider>(new ExchangeRateProvider());
		string configFilePath = Path.Combine(dataDir, "Config.json");
		Config config = Config.LoadFile(configFilePath);
		services.AddSingleton(serviceProvider => config );

		services.AddSingleton<IdempotencyRequestCache>();
		services.AddHttpClient("no-name").ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
		{
			// See https://github.com/dotnet/runtime/issues/18348#issuecomment-415845645
			PooledConnectionLifetime = TimeSpan.FromMinutes(5)
		});
		services.AddSingleton<IRPCClient>(provider =>
		{
			string host = config.GetBitcoinRpcUri();
			RPCClient rpcClient = new(
					authenticationString: config.BitcoinRpcConnectionString,
					hostOrUri: host,
					network: config.Network);

			IMemoryCache memoryCache = provider.GetRequiredService<IMemoryCache>();
			CachedRpcClient cachedRpc = new(rpcClient, memoryCache);
			return cachedRpc;
		});

		var network = config.Network;

		BlockFilterGenerator blockFilterGenerator = config.FilterType switch
		{
			"legacy" => LegacyWasabiFilterGenerator.GenerateBlockFilterAsync,
			"bip158" => BitcoinRpcBip158FilterFetcher.FetchBlockFilterAsync,
			var filterType => throw new ArgumentException($"Invalid '{filterType}'. Only 'legacy' and 'bip158' filter types are allowed.")
		};
		services.AddSingleton(_ => network);
		services.AddSingleton<MempoolService>();
		services.AddSingleton<IdempotencyRequestCache>();
		services.AddSingleton<IndexBuilderService>(s =>
			new IndexBuilderService(
				s.GetRequiredService<IRPCClient>(),
				Path.Combine(dataDir, "IndexBuilderService", $"Index{network}.sqlite"),
				blockFilterGenerator));
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

		// So to correctly handle HEAD requests.
		// https://www.tpeczek.com/2017/10/exploring-head-method-behavior-in.html
		// https://github.com/tpeczek/Demo.AspNetCore.Mvc.CosmosDB/blob/master/Demo.AspNetCore.Mvc.CosmosDB/Middlewares/HeadMethodMiddleware.cs
		app.UseMiddleware<HeadMethodMiddleware>();

		app.UseResponseCompression();

		app.UseEndpoints(endpoints => endpoints.MapControllers());
		app.UseRequestTimeouts();
	}
}
