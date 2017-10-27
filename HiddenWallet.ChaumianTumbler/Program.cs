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

namespace HiddenWallet.ChaumianTumbler
{
	public class Program
	{
#pragma warning disable IDE1006 // Naming Styles
		public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			var configFilePath = Path.Combine(Global.DataDir, "Config.json");
			if (File.Exists(configFilePath))
			{
				Global.Config = await Config.CreateFromFileAsync(configFilePath, CancellationToken.None);
			}
			else
			{
				Global.Config = new Config(
					inputRegistrationPhaseTimeoutInSeconds: 86400, // one day
					inputConfirmationPhaseTimeoutInSeconds: 60,
					outputRegistrationPhaseTimeoutInSeconds: 60,
					signingPhaseTimeoutInSeconds: 60);
				await Global.Config.ToFileAsync(configFilePath, CancellationToken.None);
				Console.WriteLine($"Config file did not exist. Created at path: {configFilePath}");
			}
			
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
