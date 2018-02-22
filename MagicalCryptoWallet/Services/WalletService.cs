using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.KeyManagement;
using MagicalCryptoWallet.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MagicalCryptoWallet.Services
{
    public class WalletService : IDisposable
    {
		#region MembersAndProperties

		public Network Network { get; }

		public KeyManager KeyManager { get; }

		public string WorkFolderPath { get; }

		public NodeConnectionParameters ConnectionParameters { get; }

		public string AddressManagerFilePath { get; }
		
		// ToDo: Does it work with RegTest? Or at least acts like it does.
		public AddressManager AddressManager { get; set; }

		public NodesGroup Nodes { get; set; }

		public MemPoolService MemPoolService { get; }

		#endregion

		#region ConstructorsAndInitializers

		public WalletService(string workFolderPath, Network network, KeyManager keyManager)
		{
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath);
			Network = Guard.NotNull(nameof(network), network);
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);

			AddressManagerFilePath = Path.Combine(WorkFolderPath, $"AddressManager{Network}.dat");
			ConnectionParameters = new NodeConnectionParameters();
			Directory.CreateDirectory(WorkFolderPath);

			try
			{
				AddressManager = AddressManager.LoadPeerFile(AddressManagerFilePath);
				Logger.LogInfo<WalletService>($"Loaded {nameof(AddressManager)} from `{AddressManagerFilePath}`.");
			}
			catch (FileNotFoundException ex)
			{
				Logger.LogInfo<WalletService>($"{nameof(AddressManager)} did not exist. Created at `{AddressManagerFilePath}`.");
				Logger.LogTrace<WalletService>(ex);
				AddressManager = new AddressManager();
			}

			//So we find nodes faster
			ConnectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(AddressManager));
			MemPoolService = new MemPoolService();
			ConnectionParameters.TemplateBehaviors.Add(new MemPoolBehavior(MemPoolService));

			Nodes = new NodesGroup(Network, ConnectionParameters,
				new NodeRequirement
				{
					RequiredServices = NodeServices.Network,
					MinVersion = ProtocolVersion.SENDHEADERS_VERSION
				})
			{
				NodeConnectionParameters = ConnectionParameters
			};
		}

		public void Start()
		{
			Nodes.Connect();
		}

		#endregion


		#region Disposing

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					try
					{
						AddressManager.SavePeerFile(AddressManagerFilePath, Network);
						Logger.LogInfo<WalletService>($"Saved {nameof(AddressManager)} to `{AddressManagerFilePath}`.");
						Nodes?.Dispose();
					}
					catch (Exception ex)
					{
						Logger.LogWarning<WalletService>(ex);
					}
				}

				_disposedValue = true;
			}
		}
		
		// ~WalletService() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// GC.SuppressFinalize(this);
		}

		#endregion
	}
}
