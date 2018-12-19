using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.WebClients;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Swagger;
using WalletWasabi.Logging;
using WalletWasabi.Interfaces;
using WalletWasabi.Backend.Middlewares;
using NBitcoin;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
				c.SwaggerDoc($"v{Helpers.Constants.BackendMajorVersion}", new Info
				{
					Version = $"v{Helpers.Constants.BackendMajorVersion}",
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
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env)
		{
			app.UseStaticFiles();

			// Enable middleware to serve generated Swagger as a JSON endpoint.
			app.UseSwagger();

			// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
			app.UseSwaggerUI(c =>
			{
				c.SwaggerEndpoint("/swagger/v2/swagger.json", "Wasabi Wallet API V2");
			});

			// So to correctly handle HEAD requests.
			// https://www.tpeczek.com/2017/10/exploring-head-method-behavior-in.html
			// https://github.com/tpeczek/Demo.AspNetCore.Mvc.CosmosDB/blob/master/Demo.AspNetCore.Mvc.CosmosDB/Middlewares/HeadMethodMiddleware.cs
			app.UseMiddleware<HeadMethodMiddleware>();

			app.UseMvc();

			var applicationLifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();
			applicationLifetime.ApplicationStopping.Register(OnShutdown); // Don't register async, that won't hold up the shutdown
		}

		private void OnShutdown()
		{
			CleanupAsync().GetAwaiter().GetResult(); // This is needed, if async function is regisered then it won't wait until it finishes
		}

		private async Task CleanupAsync()
		{
			Global.Coordinator?.Dispose();
			Logger.LogInfo<Startup>("Coordinator is disposed.");

			var stopTasks = new List<Task>();

			if (!(Global.IndexBuilderService is null))
			{
				Global.IndexBuilderService.NewBlock -= Global.IndexBuilderService_NewBlockAsync;

				var t = Global.IndexBuilderService.StopAsync();
				stopTasks.Add(t);
			}

			if (!(Global.RoundConfigWatcher is null))
			{
				var t = Global.RoundConfigWatcher.StopAsync();
				stopTasks.Add(t);
			}

			await Task.WhenAll(stopTasks);
			Logger.LogInfo<Startup>("IndexBuilderService is disposed.");
			Logger.LogInfo<Startup>("RoundConfigWatcher is disposed.");
		}
	}
}
