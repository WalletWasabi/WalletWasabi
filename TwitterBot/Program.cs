using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tweetinvi;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Logging;
using WalletWasabi.WebClients.Wasabi.ChaumianCoinJoin;

namespace TwitterBot
{
	class Program
	{
		private static CcjRunningRoundState previousState;
		private static TimedWorker _worker;
		private static Config _config;

		static async Task Main(string[] args)
		{
			Console.WriteLine("Wasabi Twitter Bot");
			Logger.InitializeDefaults("Logs.txt");
			_config = Config.LoadFromFile("Config.ini");

			var exitEvent = new AutoResetEvent(false);

			System.AppDomain.CurrentDomain.ProcessExit += (s, e) => {
				exitEvent.Set();
			};
			Console.CancelKeyPress += (s, e) => {
				e.Cancel = true;
				exitEvent.Set();
			};

			_worker = new TimedWorker();
			_worker.QueueForever(
				async()=>await CheckCoinJoinRoundStatusAsync(), 
				TimeSpan.FromSeconds(_config.Get<int>("Time-Interval")));
			_worker.Start();

			exitEvent.WaitOne();
			_worker.Stop();
		}

		static async Task CheckCoinJoinRoundStatusAsync()
		{
			var wasabiApiEndpoint = _config.Get<string>("Wasabi-URL");
			using (var satoshiClient = new SatoshiClient(new Uri(wasabiApiEndpoint)))
			{
				var states = await satoshiClient.GetAllRoundStatesAsync();
				var state = states.First();

				if(IsNewStateImportant(state))
				{
					Auth.SetUserCredentials(
						_config.Get<string>("Consumer-Key"), 
						_config.Get<string>("Consumer-Secret"), 
						_config.Get<string>("User-Access-Token"), 
						_config.Get<string>("User-Access-Secret"));
					
					Tweet.PublishTweet($"@WasabiWallet's just helped another {state.RegisteredPeerCount} people improve their financial privacy. {_config.Get<string>("Tags")}");
				}
				previousState = state;
			}
		}

		private static bool IsNewStateImportant(CcjRunningRoundState state)
		{
			return state.RegisteredPeerCount == state.RequiredPeerCount;
		}
	}
}
