using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.WebClients;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Swagger;

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
					Description = "Privacy oriented Bitcoin Web API",
					TermsOfService = "None",
					License = new License { Name = "Use under MIT", Url = "https://github.com/nopara73/WalletWasabi/blob/master/LICENSE.md" }
				});

				// Set the comments path for the Swagger JSON and UI.
				var basePath = AppContext.BaseDirectory;
				var xmlPath = Path.Combine(basePath, "WalletWasabi.Backend.xml");
				c.IncludeXmlComments(xmlPath);
			});

			services.AddSingleton<IExchangeRateProvider>(new ExchangeRateProvider() );
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
			applicationLifetime.ApplicationStopping.Register(OnShutdownAsync);
		}

		private async void OnShutdownAsync()
		{
			if(Global.IndexBuilderService != null)
			{
				await Global.IndexBuilderService.StopAsync();
			}
		}
	}
}
