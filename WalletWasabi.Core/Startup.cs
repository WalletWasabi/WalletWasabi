using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Core
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
			services.AddMvc(options => options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(BitcoinAddress))))
				.AddControllersAsServices();

			services.AddMvc()
				.AddNewtonsoftJson();

			services.AddControllers()
				.AddNewtonsoftJson();

			// Register the Swagger generator, defining one or more Swagger documents
			services.AddSwaggerGen(c =>
			{
				c.SwaggerDoc(
					$"v{Constants.WasabiCoreMajorVersion}",
					new OpenApiInfo
					{
						Version = $"v{Constants.WasabiCoreMajorVersion}",
						Title = "Wasabi Wallet Core",
						Description = "Privacy focused Bitcoin wallet.",
						License = new OpenApiLicense { Name = "Use under MIT.", Url = new Uri("https://github.com/zkSNACKs/WalletWasabi/blob/master/LICENSE.md") }
					});

				// Set the comments path for the Swagger JSON and UI.
				var basePath = AppContext.BaseDirectory;
				var xmlPath = Path.Combine(basePath, "WalletWasabi.Core.xml");
				c.IncludeXmlComments(xmlPath);
			});

			services.AddLogging(logging => logging.AddFilter((s, level) => level >= Microsoft.Extensions.Logging.LogLevel.Warning));

			services.AddSingleton(new Global(Configuration["datadir"]));
			services.AddStartupTask<InitConfigStartupTask>();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
#pragma warning disable IDE0060 // Remove unused parameter

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, Global global)
#pragma warning restore IDE0060 // Remove unused parameter
		{
			// Enable middleware to serve generated Swagger as a JSON endpoint.
			app.UseSwagger();

			// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
			app.UseSwaggerUI(c => c.SwaggerEndpoint($"/swagger/v{Constants.WasabiCoreMajorVersion}/swagger.json", $"Wasabi Core API V{Constants.WasabiCoreMajorVersion}"));

			app.UseRouting();

			app.UseEndpoints(endpoints => endpoints.MapControllers());

			var applicationLifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
			applicationLifetime.ApplicationStopping.Register(() => OnShutdown(global)); // Don't register async, that won't hold up the shutdown
		}

		private void OnShutdown(Global global)
		{
			CleanupAsync(global).GetAwaiter().GetResult(); // This is needed, if async function is registered then it won't wait until it finishes
		}

		private async Task CleanupAsync(Global global)
		{
			Logger.LogSoftwareStopped("Wasabi Core");
		}
	}
}
