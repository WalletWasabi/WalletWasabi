using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Backend.Middlewares;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.WebClients;

namespace WalletWasabi.Backend
{
	public class Startup
	{
		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddMemoryCache();

			services.AddMvc(options =>
			{
				options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(BitcoinAddress)));
			})
				.AddControllersAsServices();

			// Register the Swagger generator, defining one or more Swagger documents
			services.AddSwaggerGen(c =>
			{
				c.SwaggerDoc($"v{Constants.BackendMajorVersion}", new Info {
					Version = $"v{Constants.BackendMajorVersion}",
					Title = "Wasabi Wallet API",
					Description = "Privacy focused, ZeroLink compliant Bitcoin Web API.",
					License = new License { Name = "Use under MIT.", Url = "https://github.com/zkSNACKs/WalletWasabi/blob/master/LICENSE.md" }
				});

				// Set the comments path for the Swagger JSON and UI.
				var basePath = AppContext.BaseDirectory;
				var xmlPath = Path.Combine(basePath, "WalletWasabi.Backend.xml");
				c.IncludeXmlComments(xmlPath);
			});

			services.AddLogging(Logging => Logging.AddFilter((s, level) => level >= Microsoft.Extensions.Logging.LogLevel.Warning));

			services.AddSingleton<IExchangeRateProvider>(new ExchangeRateProvider());
			services.AddSingleton<Global>(new Global());

			services.AddResponseCompression();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
#pragma warning disable IDE0060 // Remove unused parameter

		public void Configure(IApplicationBuilder app, IHostingEnvironment env, Global backendGlobal)
#pragma warning restore IDE0060 // Remove unused parameter
		{
			app.UseStaticFiles();

			// Enable middleware to serve generated Swagger as a JSON endpoint.
			app.UseSwagger();

			// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
			app.UseSwaggerUI(c =>
			{
				c.SwaggerEndpoint($"/swagger/v{Constants.BackendMajorVersion}/swagger.json", "Wasabi Wallet API V3");
			});

			// So to correctly handle HEAD requests.
			// https://www.tpeczek.com/2017/10/exploring-head-method-behavior-in.html
			// https://github.com/tpeczek/Demo.AspNetCore.Mvc.CosmosDB/blob/master/Demo.AspNetCore.Mvc.CosmosDB/Middlewares/HeadMethodMiddleware.cs
			app.UseMiddleware<HeadMethodMiddleware>();

			app.UseResponseCompression();

			app.UseMvc();

			var applicationLifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();
			applicationLifetime.ApplicationStopping.Register(() => OnShutdown(backendGlobal)); // Don't register async, that won't hold up the shutdown
		}

		private void OnShutdown(Global backendGlobal)
		{
			CleanupAsync(backendGlobal).GetAwaiter().GetResult(); // This is needed, if async function is registered then it won't wait until it finishes
		}

		private async Task CleanupAsync(Global backendGlobal)
		{
			backendGlobal.Coordinator?.Dispose();
			Logger.LogInfo<Startup>("Coordinator is disposed.");

			if (backendGlobal.IndexBuilderService != null)
			{
				await backendGlobal.IndexBuilderService.StopAsync();
				Logger.LogInfo<Startup>("IndexBuilderService is disposed.");
			}

			if (backendGlobal.RoundConfigWatcher != null)
			{
				await backendGlobal.RoundConfigWatcher.StopAsync();
				Logger.LogInfo<Startup>("RoundConfigWatcher is disposed.");
			}

			if (backendGlobal.LocalNode != null)
			{
				backendGlobal.DisconnectDisposeNullLocalNode();
				Logger.LogInfo<Startup>("LocalNode is disposed.");
			}

			Logger.LogInfo($"Wasabi Backend stopped gracefully.", Logger.InstanceGuid.ToString());
		}
	}
}
