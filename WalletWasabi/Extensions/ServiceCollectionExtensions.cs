using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WalletWasabi.Services;

namespace WalletWasabi.Extensions;

public static class IServiceCollectionExtensions
{
	public static IServiceCollection AddBackgroundService<TService>(this IServiceCollection services) where TService : class, IHostedService =>
		services.AddSingleton<TService>().AddHostedService<BackgroundServiceStarter<TService>>();
}
