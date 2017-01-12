using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;

namespace HiddenWallet.KeyManagement
{
	public class Safe
	{
		private Network _network;
		public Network Network => _network;
		private ExtKey _extKey;
		public ExtKey ExtKey => _extKey;
		public BitcoinExtPubKey BitcoinExtPubKey => ExtKey.Neuter().GetWif(Network);
		public BitcoinExtKey BitcoinExtKey => ExtKey.GetWif(Network);

		public BitcoinAddress GetAddress(int index, HdPathType hdPathType = HdPathType.Receive)
		{
			return GetPrivateKey(index, hdPathType).ScriptPubKey.GetDestinationAddress(Network);
		}

		public HashSet<BitcoinAddress> GetFirstNAddresses(int addressCount, HdPathType hdPathType = HdPathType.Receive)
		{
			var addresses = new HashSet<BitcoinAddress>();

			for (var i = 0; i < addressCount; i++)
			{
				addresses.Add(GetAddress(i, hdPathType));
			}

			return addresses;
		}

		// Let's generate the walletname from seedpublickey
		// Let's get the pubkey, so the chaincode is lost
		// Let's get the address, you can't directly access it from the safe
		// Also nobody would ever use this address for anything
		public string UniqueId => BitcoinExtPubKey.ExtPubKey.PubKey.GetAddress(Network).ToWif();

		public string WalletFilePath { get; }

		protected Safe(string password, string walletFilePath, Network network, string mnemonicString = null)
		{
			_network = network;

			if (mnemonicString != null)
			{
				SetSeed(password, mnemonicString);
			}

			WalletFilePath = walletFilePath;
		}

		public Safe(Safe safe)
		{
			_network = safe.Network;
			_extKey = safe.ExtKey;
			WalletFilePath = safe.WalletFilePath;
		}

		/// <summary>
		///     Creates a mnemonic, a seed, encrypts it and stores in the specified path.
		/// </summary>
		/// <param name="mnemonic">empty string</param>
		/// <param name="password"></param>
		/// <param name="walletFilePath"></param>
		/// <param name="network"></param>
		/// <returns>Safe</returns>
		public static Safe Create(out string mnemonic, string password, string walletFilePath, Network network)
		{
			var safe = new Safe(password, walletFilePath, network);

			mnemonic = safe.SetSeed(password).ToString();

			safe.Save(password, walletFilePath, network);

			return safe;
		}

		public static Safe Recover(string mnemonic, string password, string walletFilePath, Network network)
		{
			var safe = new Safe(password, walletFilePath, network, mnemonic);
			safe.Save(password, walletFilePath, network);
			return safe;
		}

		private Mnemonic SetSeed(string password, string mnemonicString = null)
		{
			var mnemonic =
				mnemonicString == null
					? new Mnemonic(Wordlist.English, WordCount.Twelve)
					: new Mnemonic(mnemonicString);

			_extKey = mnemonic.DeriveExtKey(password);

			return mnemonic;
		}

		private void SetSeed(ExtKey seedExtKey)
		{
			_extKey = seedExtKey;
		}

		private void Save(string password, string walletFilePath, Network network)
		{
			if (File.Exists(walletFilePath))
				throw new Exception("WalletFileAlreadyExists");

			var directoryPath = Path.GetDirectoryName(Path.GetFullPath(walletFilePath));
			if (directoryPath != null) Directory.CreateDirectory(directoryPath);

			var privateKey = ExtKey.PrivateKey;
			var chainCode = ExtKey.ChainCode;

			var encryptedBitcoinPrivateKeyString = privateKey.GetEncryptedBitcoinSecret(password, Network).ToWif();
			var chainCodeString = Convert.ToBase64String(chainCode);

			var networkString = network.ToString();

			WalletFileSerializer.Serialize(walletFilePath,
				encryptedBitcoinPrivateKeyString,
				chainCodeString,
				networkString);
		}

		public static Safe Load(string password, string walletFilePath)
		{
			if (!File.Exists(walletFilePath))
				throw new Exception("WalletFileDoesNotExists");

			var walletFileRawContent = WalletFileSerializer.Deserialize(walletFilePath);

			var encryptedBitcoinPrivateKeyString = walletFileRawContent.EncryptedSeed;
			var chainCodeString = walletFileRawContent.ChainCode;

			var chainCode = System.Convert.FromBase64String(chainCodeString);

			Network network;
			var networkString = walletFileRawContent.Network;
			if (networkString == Network.Main.ToString())
				network = Network.Main;
			else if (networkString == Network.TestNet.ToString())
				network = Network.TestNet;
			else throw new Exception("NotRecognizedNetworkInWalletFile");

			var safe = new Safe(password, walletFilePath, network);

			var privateKey = Key.Parse(encryptedBitcoinPrivateKeyString, password, safe.Network);
			var seedExtKey = new ExtKey(privateKey, chainCode);
			safe.SetSeed(seedExtKey);

			return safe;
		}

		#region Hierarchy

		private const string StealthPath = "0'";
		private const string ReceiveHdPath = "1'";
		private const string ChangeHdPath = "2'";
		private const string NonHardenedHdPath = "3";

		public enum HdPathType
		{
			Receive,
			Change,
			NonHardened
		}

		#endregion Hierarchy

		internal BitcoinExtKey FindPrivateKey(BitcoinAddress address, int stopSearchAfterIteration = 100000)
		{
			for (int i = 0; i < stopSearchAfterIteration; i++)
			{
				if (GetAddress(i, HdPathType.Receive) == address)
					return GetPrivateKey(i, HdPathType.Receive);
				if (GetAddress(i, HdPathType.Change) == address)
					return GetPrivateKey(i, HdPathType.Change);
				if (GetAddress(i, HdPathType.NonHardened) == address)
					return GetPrivateKey(i, HdPathType.NonHardened);
			}
			throw new Exception("Bitcoin address not found.");
		}

		internal BitcoinExtKey GetPrivateKey(int index, HdPathType hdPathType = HdPathType.Receive)
		{
			KeyPath keyPath;
			if (hdPathType == HdPathType.Receive)
			{
				keyPath = new KeyPath($"{ReceiveHdPath}/{index}'");
			}
			else if (hdPathType == HdPathType.Change)
			{
				keyPath = new KeyPath($"{ChangeHdPath}/{index}'");
			}
			else if (hdPathType == HdPathType.NonHardened)
			{
				keyPath = new KeyPath($"{NonHardenedHdPath}/{index}");
			}
			else throw new Exception("HdPathType not exists");

			return ExtKey.Derive(keyPath).GetWif(Network);
		}
	}
}