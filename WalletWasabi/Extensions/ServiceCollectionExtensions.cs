using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WalletWasabi.Extensions;

public static class IServiceCollectionExtensions
{
	public static IServiceCollection AddBackgroundService<TService>(this IServiceCollection services) where TService : class, IHostedService =>
		services
			.AddSingleton<TService>()
			.AddHostedService(sp => sp.GetRequiredService<TService>());

	public static IServiceCollection AddBackgroundService<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory) where TService : class, IHostedService =>
		services
			.AddSingleton(implementationFactory)
			.AddHostedService(sp => sp.GetRequiredService<TService>());
}
