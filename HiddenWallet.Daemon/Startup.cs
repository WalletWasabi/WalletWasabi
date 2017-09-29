using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;

namespace HiddenWallet.Daemon
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {

        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
			// Add framework services.
			services.AddMvc().AddJsonOptions(options =>
			{
				//return json format with Camel Case
				options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver();
			});
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app)
        {
			app.UseMvc();

			var applicationLifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();
			applicationLifetime.ApplicationStopping.Register(OnShutdown);
		}

		private void OnShutdown()
		{
			Global.WalletWrapper.EndAsync().Wait();
		}
	}
}
