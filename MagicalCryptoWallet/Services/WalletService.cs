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
	public class WalletService
	{
		#region MembersAndProperties

		public Network Network { get; }

		public KeyManager KeyManager { get; }

		public string WorkFolderPath { get; }

		public NodesGroup Nodes { get; }

		public MemPoolService MemPoolService { get; }

		#endregion

		#region ConstructorsAndInitializers

		public WalletService(string workFolderPath, Network network, KeyManager keyManager, NodesGroup nodes, MemPoolService mempoolService)
		{
			WorkFolderPath = Guard.NotNullOrEmptyOrWhitespace(nameof(workFolderPath), workFolderPath);
			Network = Guard.NotNull(nameof(network), network);
			KeyManager = Guard.NotNull(nameof(keyManager), keyManager);
			Nodes = Guard.NotNull(nameof(nodes), nodes);
			MemPoolService = Guard.NotNull(nameof(mempoolService), mempoolService);

			Directory.CreateDirectory(WorkFolderPath);
		}

		#endregion
	}
}
