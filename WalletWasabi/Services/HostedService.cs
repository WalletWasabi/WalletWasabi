using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Services
{
	public class HostedService
	{
		public IHostedService Service { get; }
		public string FriendlyName { get; }

		public HostedService(IHostedService service, string friendlyName)
		{
			Service = Guard.NotNull(nameof(service), service);
			FriendlyName = Guard.NotNull(nameof(friendlyName), friendlyName);
		}
	}
}
