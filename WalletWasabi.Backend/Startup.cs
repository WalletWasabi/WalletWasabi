using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin.RPC;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using NBitcoin;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;

[assembly: ApiController]

namespace WalletWasabi.Backend;

public class Startup
{
	public Startup(IConfiguration configuration)
	{
		Configuration = configuration;
	}

	private IConfiguration Configuration { get; }

	// This method gets called by the runtime. Use this method to add services to the container.
	public void ConfigureServices(IServiceCollection services)
	{
		string dataDir = Configuration["datadir"] ?? EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Backend"));
		Logger.InitializeDefaults(Path.Combine(dataDir, "Logs.txt"));

		string configFilePath = Path.Combine(dataDir, "Config.json");
		Config config = Config.LoadFile(configFilePath);
		services.AddSingleton(serviceProvider => config );

		services.AddSingleton<IRPCClient>(provider =>
		{
			string host = config.GetBitcoinRpcUri();
			RPCClient rpcClient = new(
					authenticationString: config.BitcoinRpcConnectionString,
					hostOrUri: host,
					network: config.Network);

			RpcClientBase rpc = new(rpcClient);
			return rpc;
		});

		var network = config.Network;

		BlockFilterGenerator blockFilterGenerator = config.FilterType switch
		{
			"legacy" => LegacyWasabiFilterGenerator.GenerateBlockFilterAsync,
			"bip158" => BitcoinRpcBip158FilterFetcher.FetchBlockFilterAsync,
			var filterType => throw new ArgumentException($"Invalid '{filterType}'. Only 'legacy' and 'bip158' filter types are allowed.")
		};
		services.AddSingleton(_ => network);
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

		app.UseResponseCompression();

		app.UseEndpoints(endpoints =>
		{
			endpoints.MapGet("/api/software/versions", () => Encode.VersionsResponse(new VersionsResponse(Constants.BackendMajorVersion)));
			endpoints.MapGet("/api/v4/btc/blockchain/filters", async (string bestKnownBlockHash, int count, CancellationToken cancellationToken) =>
			{
				var indexBuilderService = app.ApplicationServices.GetRequiredService<IndexBuilderService>();
				return await GetFiltersAsync(indexBuilderService, bestKnownBlockHash, count, cancellationToken).ConfigureAwait(false);
			});
		});
		app.UseRequestTimeouts();
	}

	private static async Task<IResult> GetFiltersAsync(IndexBuilderService indexBuilderService, string bestKnownBlockHash, int count, CancellationToken cancellationToken)
	{
		if (count <= 0)
		{
			return Results.BadRequest("Invalid block hash or count is provided.");
		}

		var knownHash = new uint256(bestKnownBlockHash);

		var (bestHeight, filters, found) = await indexBuilderService.GetFilterLinesExcludingAsync(knownHash, count, cancellationToken);

		if (!found)
		{
			return Results.NotFound($"Provided {nameof(bestKnownBlockHash)} is not found: {bestKnownBlockHash}.");
		}

		if (!filters.Any())
		{
			return Results.NoContent();
		}

		var response = new FiltersResponse
		{
			BestHeight = bestHeight,
			Filters = filters
		};

		return Results.Ok(Encode.FiltersResponse(response));
	}
}
