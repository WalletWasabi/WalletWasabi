﻿using System;
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

namespace WalletWasabi.Backend
{
	public class Startup
	{
		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddMemoryCache();
			services.AddMvc();

			// Register the Swagger generator, defining one or more Swagger documents
			services.AddSwaggerGen(c =>
			{
				c.SwaggerDoc("v1", new Info
				{
					Version = "v1",
					Title = "Wallet Wasabi API",
					Description = "Privacy oriented Bitcoin Web API.",
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
			// Enable middleware to serve generated Swagger as a JSON endpoint.
			app.UseSwagger();

			// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
			app.UseSwaggerUI(c =>
			{
				c.SwaggerEndpoint("/swagger/v1/swagger.json", "Wallet Wasabi API V1");
			});

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

			if (Global.IndexBuilderService != null)
			{
				Global.IndexBuilderService.NewBlock -= Global.IndexBuilderService_NewBlockAsync;

				var t = Global.IndexBuilderService.StopAsync();
				stopTasks.Add(t);
			}

			if (Global.RoundConfigWatcher != null)
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
