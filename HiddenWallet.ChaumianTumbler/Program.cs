using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using HiddenWallet.ChaumianTumbler.Denomination;
using NBitcoin;

namespace HiddenWallet.ChaumianTumbler
{
	public class Program
	{
#pragma warning disable IDE1006 // Naming Styles
		public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			var configFilePath = Path.Combine(Global.DataDir, "Config.json");
			Global.Config = new Config();
			await Global.Config.LoadOrCreateDefaultFileAsync(configFilePath, CancellationToken.None);

			Global.StateMachine = new TumblerStateMachine();
			Global.StateMachineJobCancel = new CancellationTokenSource();
			Global.StateMachineJob = Global.StateMachine.StartAsync(Global.StateMachineJobCancel.Token);

			using (var host = WebHost.CreateDefaultBuilder(args)
				.UseStartup<Startup>()
				.Build())
			{
				await host.RunAsync();
			}
		}
	}
}
