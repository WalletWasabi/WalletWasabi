using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.SignalR;
using System.IO;
using Swashbuckle.AspNetCore.Swagger;
using Microsoft.AspNetCore.HttpOverrides;

namespace HiddenWallet.ChaumianTumbler
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
			services.AddSignalR(); //Must come before .AddMvc()
			services.AddMvc();
			services.AddSwaggerGen(c =>
			{
				c.SwaggerDoc("v1", new Info { Title = "HiddenWallet.ChaumianTumbler", Version = "v1" });
			});
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, IHubContext<TumblerHub> context)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}
			
			app.UseSignalR(routes =>
		   {
			   routes.MapHub<TumblerHub>("chaumianTumbler");
		   }); //Must come before .UseMvc()

			app.UseMvc();

			var applicationLifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();
			applicationLifetime.ApplicationStopping.Register(OnShutdownAsync);

			app.UseSwagger();

			// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
			app.UseSwaggerUI(c =>
			{
				c.SwaggerEndpoint("/swagger/v1/swagger.json", "HiddenWallet.ChaumianTumbler V1");
			});

			NotificationBroadcaster notificationBroadcast = NotificationBroadcaster.Instance;
			notificationBroadcast.SignalRHub = context;
		}

		private async void OnShutdownAsync()
		{
			Global.StateMachineJobCancel?.Cancel();
			if(Global.StateMachineJob != null)
			{
				await Global.StateMachineJob;
			}
			Global.StateMachine?.Dispose();
		}
	}
}
