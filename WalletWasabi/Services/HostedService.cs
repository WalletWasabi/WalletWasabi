using Microsoft.Extensions.Hosting;

namespace WalletWasabi.Services;

public class HostedService
{
	public HostedService(IHostedService service, string friendlyName)
	{
		Service = service;
		FriendlyName = friendlyName;
	}

	public IHostedService Service { get; }
	public string FriendlyName { get; }
}
