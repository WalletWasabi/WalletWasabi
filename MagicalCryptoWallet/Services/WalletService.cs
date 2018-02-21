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

		// ToDo: I am accessing it a different way than HiddenWallet/Nicolas's SPV sample
		// He has a GetAddressManager(), which always looks for AddressManager inside ConnectionParameters.TemplateBehaviors
		// Not sure if I had a reason to do this, or I just used Nicolas's legacy
		// ToDo: Does it work with RegTest? Or at least acts like it does.
		public AddressManager AddressManager { get; private set; }

		public NodesGroup Nodes { get; private set; }

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
			}
			catch (Exception ex) // ToDo: find out what the specific exception that is thrown and catch that
			{
				Logger.LogTrace<WalletService>(ex);
				AddressManager = new AddressManager();
			}

			//So we find nodes faster
			ConnectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(AddressManager));

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
						Nodes?.Dispose();
					}
					catch (Exception ex)
					{
						Logger.LogWarning<WalletService>(ex);
					}
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				_disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~WalletService() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}

		#endregion
	}
}
