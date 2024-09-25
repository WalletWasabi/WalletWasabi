using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using WalletWasabi.Backend.Controllers;
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
		app.UseRouting();
		app.UseEndpoints(endpoints => endpoints.MapControllers());
	}

	public void ConfigureServices(IServiceCollection services)
	{
		var backendAssembly = typeof(WabiSabiController).Assembly;
		services.AddSingleton<IdempotencyRequestCache>();
		services
			.AddMvc(options =>
			{
				options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(BitcoinAddress)));
				options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(Script)));
			})
			.ConfigureApplicationPartManager(manager =>
			{
				manager.FeatureProviders.Add(new ControllerProvider(Configuration));
			})
			.AddApplicationPart(backendAssembly)
			.AddNewtonsoftJson(x => x.SerializerSettings.Converters = JsonSerializationOptions.Default.Settings.Converters);
	}
}
public class ControllerProvider : ControllerFeatureProvider
{
    public readonly IConfiguration _configuration;

    public ControllerProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override bool IsController(TypeInfo typeInfo)
    {
	    return typeInfo.Name.Contains(nameof(WabiSabiController));
    }
}
