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
			using (var host = WebHost.CreateDefaultBuilder(args)
				.UseStartup<Startup>()
				.Build())
			using(var ctsSource = new CancellationTokenSource())
			{
				Global.StateMachine = new TumblerStateMachine();
				var stateMachineTask = Global.StateMachine.StartAsync(ctsSource.Token);
				await host.RunAsync();
				ctsSource.Cancel();
				await stateMachineTask;
			}
		}
    }
}
