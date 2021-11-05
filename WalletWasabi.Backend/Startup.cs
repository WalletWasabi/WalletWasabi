using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using NBitcoin;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Middlewares;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models.Serialization;
using WalletWasabi.WebClients;

[assembly: ApiController]

namespace WalletWasabi.Backend
{
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
			services.AddMemoryCache();

			services.AddMvc(options => options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(BitcoinAddress))))
				.AddControllersAsServices();

			services.AddMvc()
				.AddNewtonsoftJson();

			services.AddControllers().AddNewtonsoftJson(x =>
			{
				x.SerializerSettings.Converters = JsonSerializationOptions.Default.Settings.Converters;
			});

			// Register the Swagger generator, defining one or more Swagger documents
			services.AddSwaggerGen(c =>
			{
				c.SwaggerDoc($"v{Constants.BackendMajorVersion}", new OpenApiInfo
				{
					Version = $"v{Constants.BackendMajorVersion}",
					Title = "Wasabi Wallet API",
					Description = "Privacy focused Bitcoin Web API.",
					License = new OpenApiLicense { Name = "Use under MIT.", Url = new Uri("https://github.com/zkSNACKs/WalletWasabi/blob/master/LICENSE.md") }
				});

				// Set the comments path for the Swagger JSON and UI.
				var basePath = AppContext.BaseDirectory;
				var xmlPath = Path.Combine(basePath, "WalletWasabi.Backend.xml");
				c.IncludeXmlComments(xmlPath);
			});

			services.AddLogging(logging => logging.AddFilter((s, level) => level >= Microsoft.Extensions.Logging.LogLevel.Warning));

			services.AddSingleton<IExchangeRateProvider>(new ExchangeRateProvider());
			services.AddSingleton(new Global(Configuration["datadir"]));
			services.AddSingleton(serviceProvider =>
			{
				var global = serviceProvider.GetRequiredService<Global>();
				var coordinator = global.HostedServices.Get<WabiSabiCoordinator>();
				return coordinator.Arena;
			});
			services.AddStartupTask<InitConfigStartupTask>();

			services.AddResponseCompression();
		}

		[SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "This method gets called by the runtime. Use this method to configure the HTTP request pipeline")]
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, Global global)
		{
			app.UseStaticFiles();

			// Enable middleware to serve generated Swagger as a JSON endpoint.
			app.UseSwagger();

			// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
			app.UseSwaggerUI(c => c.SwaggerEndpoint($"/swagger/v{Constants.BackendMajorVersion}/swagger.json", $"Wasabi Wallet API V{Constants.BackendMajorVersion}"));

			app.UseRouting();

			// So to correctly handle HEAD requests.
			// https://www.tpeczek.com/2017/10/exploring-head-method-behavior-in.html
			// https://github.com/tpeczek/Demo.AspNetCore.Mvc.CosmosDB/blob/master/Demo.AspNetCore.Mvc.CosmosDB/Middlewares/HeadMethodMiddleware.cs
			app.UseMiddleware<HeadMethodMiddleware>();

			app.UseResponseCompression();

			app.UseEndpoints(endpoints => endpoints.MapControllers());

			var applicationLifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
			applicationLifetime.ApplicationStopped.Register(() => OnShutdown(global)); // Don't register async, that won't hold up the shutdown
		}

		private void OnShutdown(Global global)
		{
			CleanupAsync(global).GetAwaiter().GetResult(); // This is needed, if async function is registered then it won't wait until it finishes
		}

		private async Task CleanupAsync(Global global)
		{
			var coordinator = global.Coordinator;
			if (coordinator is { })
			{
				coordinator.Dispose();
				Logger.LogInfo($"{nameof(coordinator)} is disposed.");
			}

			var indexBuilderService = global.IndexBuilderService;
			if (indexBuilderService is { })
			{
				await indexBuilderService.StopAsync();
				Logger.LogInfo($"{nameof(indexBuilderService)} is stopped.");
			}

			var hostedServices = global.HostedServices;
			if (hostedServices is { })
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(21));
				await hostedServices.StopAllAsync(cts.Token);
				hostedServices.Dispose();
			}

			var p2pNode = global.P2pNode;
			if (p2pNode is { })
			{
				await p2pNode.DisposeAsync();
				Logger.LogInfo($"{nameof(p2pNode)} is disposed.");
			}

			Logger.LogSoftwareStopped("Wasabi Backend");
		}
	}
}
