using Microsoft.Extensions.Hosting;
using WalletWasabi.Helpers;

namespace WalletWasabi.Services;

public class HostedService
{
	public HostedService(IHostedService service, string friendlyName)
	{
		Service = Guard.NotNull(nameof(service), service);
		FriendlyName = Guard.NotNull(nameof(friendlyName), friendlyName);
	}

	public IHostedService Service { get; }
	public string FriendlyName { get; }
}
