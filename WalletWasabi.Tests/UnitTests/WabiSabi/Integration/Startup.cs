using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using WalletWasabi.Cache;
using WalletWasabi.Coordinator;
using WalletWasabi.Coordinator.Controllers;
using WalletWasabi.Serialization;

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
		var indexerAssembly = typeof(WabiSabiController).Assembly;
		services.AddSingleton<IdempotencyRequestCache>();
		services
			.AddMvc(options =>
			{
				options.InputFormatters.Insert(0, new WasabiJsonInputFormatter(Decode.CoordinatorMessageFromStreamAsync));
				options.OutputFormatters.Insert(0, new WasabiJsonOutputFormatter(Encode.CoordinatorMessage));
				options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(Script)));
			})
			.ConfigureApplicationPartManager(manager =>
			{
				manager.FeatureProviders.Add(new ControllerProvider(Configuration));
			})
			.AddApplicationPart(indexerAssembly);
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
