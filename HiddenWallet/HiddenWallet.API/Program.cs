using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http;
using HiddenWallet.API.Wrappers;

namespace HiddenWallet.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
			try
			{
				Global.WalletWrapper = new WalletWrapper();

				var host = new WebHostBuilder()
					.UseKestrel()
					.UseContentRoot(Directory.GetCurrentDirectory())
					.UseStartup<Startup>()
					.Build();

				host.Run();
			}
			finally
			{
				Global.WalletWrapper.ShutdownAsync().Wait(); // won't happen if process is killed
			}
        }
    }
}
