using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
#if DEBUG
using Microsoft.OpenApi.Models;
#endif
using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
#if DEBUG
using System.Xml.XPath;
#endif
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Crypto.Serialization;
using WalletWasabi.WabiSabi.Models.Serialization;
using WalletWasabi.WabiSabiClientLibrary.Crypto;
#if DEBUG
using WalletWasabi.WabiSabiClientLibrary.Middlewares;
#endif

namespace WalletWasabi.WabiSabiClientLibrary;


public class Program
{
	public static async Task Main(string[] args)
	{

		Logger.SetModes(LogMode.Console);
		Logger.SetMinimumLevel(Logging.LogLevel.Info);

		try
		{
			Global global = new Global();
			string? portString = Environment.GetEnvironmentVariable("WCL_BIND_PORT");
			int port = portString is null ? 37128 : int.Parse(portString);
			string url = $"http://localhost:{port}";

			WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
			builder.Services.AddControllers().AddNewtonsoftJson(x =>
			{
				x.SerializerSettings.Converters = JsonSerializationOptions.Default.Settings.Converters;
				x.SerializerSettings.Converters.Add(new ZeroCredentialsRequestJsonConverter());
				x.SerializerSettings.Converters.Add(new RealCredentialsRequestJsonConverter());
				x.SerializerSettings.ContractResolver = JsonSerializationOptions.Default.Settings.ContractResolver;
			}).ConfigureApiBehaviorOptions(options =>
			{
				options.SuppressModelStateInvalidFilter = true;
			});
			builder.Services.AddSingleton<Global>(global);
#if (DEBUG)
			builder.Services.AddSingleton<WasabiRandom>(new DeterministicRandom(0));
#else
			builder.Services.AddSingleton<WasabiRandom>(new SecureRandom());
#endif

			builder.Logging.ClearProviders();
#if DEBUG
			builder.Services.AddSwaggerGen(c =>
			{
				c.CustomSchemaIds(type => type.ToString());
				c.SwaggerDoc($"v{global.Version}", new OpenApiInfo
				{
					Version = $"v{global.Version}",
					Title = "WabiSabiClientLibrary API",
				});
				c.IncludeXmlComments(() =>
				{
					Assembly assembly = Assembly.GetExecutingAssembly();
					using Stream stream = assembly.GetManifestResourceStream("documentation.xml")!;
					return new XPathDocument(stream);
				});
			});
#endif

			WebApplication app = builder.Build();
#if DEBUG
			app.UseMiddleware<RequestLoggerMiddleware>();
			app.UseMiddleware<ResponseLoggerMiddleware>();
#endif
			app.Urls.Add(url);
			app.MapControllers();
#if DEBUG
			app.UseSwagger();
			app.UseSwaggerUI(c => c.SwaggerEndpoint($"/swagger/v{global.Version}/swagger.json", $"WabiSabiClientLibrary API V{global.Version}"));
#endif
			app.Lifetime.ApplicationStarted.Register(() => OnStartup(url, global));
			app.Lifetime.ApplicationStopped.Register(() => OnShutdown());

			await app.RunAsync();
		}

		catch (Exception exception)
		{
			Logger.LogCritical(exception);
		}
	}

	private static void OnStartup(string url, Global global)
	{
		Logger.LogInfo($"WabSabi Client Library has started");
		Logger.LogInfo($"Version: {global.Version}");
		Logger.LogInfo($"Commit hash: {global.CommitHash}");
		Logger.LogInfo($"Debug: {global.Debug}");
		Logger.LogInfo($"Bind address: {url}");
	}


	private static void OnShutdown()
	{
		Logger.LogInfo($"WabiSabi Client Library has been stopped gracefully");
	}
}
