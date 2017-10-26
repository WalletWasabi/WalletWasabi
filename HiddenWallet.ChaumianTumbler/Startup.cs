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
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		// Inject IHubContext so MVC and ChaumianTumbler class can access the hub context
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IHubContext<ChaumianTumblerHub> context)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

			app.UseSignalR(routes =>
			{
				routes.MapHub<ChaumianTumblerHub>("chaumianTumbler");
			}); //Must come before .UseMvc()

			app.UseMvc();

			//	Set the context within the ChaumianTumble singleton
			ChaumianTumbler ct = ChaumianTumbler.Instance;
			ct.ChatHub = context;
		}
    }
}
