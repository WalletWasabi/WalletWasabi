using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using WalletWasabi.Cache;
using WalletWasabi.WabiSabi.Models.Serialization;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Integration;

public class Startup
{
	public Startup(IConfiguration configuration)
	{
		Configuration = configuration;
	}

	public IConfiguration Configuration { get; }

	public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
	{
		app.UseStaticFiles();
		app.UseRouting();
		app.UseEndpoints(endpoints => endpoints.MapControllers());
	}

	public void ConfigureServices(IServiceCollection services)
	{
		var backendAssembly = typeof(WalletWasabi.Backend.Controllers.WabiSabiController).Assembly;
		services.AddSingleton<IdempotencyRequestCache>();
		services
			.AddMvc(options =>
			{
				options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(BitcoinAddress)));
				options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(Script)));
			})
			.AddApplicationPart(backendAssembly)
			.AddControllersAsServices()
			.AddNewtonsoftJson(x => x.SerializerSettings.Converters = JsonSerializationOptions.Default.Settings.Converters);
	}
}
