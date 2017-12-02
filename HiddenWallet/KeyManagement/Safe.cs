using ConcurrentCollections;
using HiddenWallet.Models;
using NBitcoin;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace HiddenWallet.KeyManagement
{
	public class Safe
	{
		public Network Network { get; }

		public static DateTimeOffset EarliestPossibleCreationTime { get; set; } = DateTimeOffset.ParseExact("2017-02-19", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

		public DateTimeOffset CreationTime { get; }

		public ExtKey ExtKey { get; private set; }
		public BitcoinExtKey BitcoinExtKey => ExtKey.GetWif(Network);
		public BitcoinExtPubKey GetBitcoinExtPubKey(int? index = null, HdPathType hdPathType = HdPathType.Receive, SafeAccount account = null) => GetBitcoinExtKey(index, hdPathType, account).Neuter();

        public PubKey GetPubKey(int index, HdPathType hdPathType, SafeAccount account)
        {
            return GetBitcoinExtKey(index, hdPathType, account).PrivateKey.PubKey;
        }

        private ConcurrentDictionary<(AddressType Type, int Index, HdPathType HdPathType, SafeAccount Account), BitcoinAddress> _safeCache = new ConcurrentDictionary<(AddressType Type, int Index, HdPathType HdPathType, SafeAccount Account), BitcoinAddress>();
        // GetP2wpkh pubKey.WitHash.ScriptPubKey.GetDestinationAddress(Network);
        // GetP2pkhAddress pubKey.Hash.ScriptPubKey.GetDestinationAddress(Network);
        // GetP2shOverP2wpkhAddress pubKey.WitHash.ScriptPubKey.Hash.ScriptPubKey.GetDestinationAddress(Network);
        public BitcoinAddress GetAddress(AddressType type, int index, HdPathType hdPathType = HdPathType.Receive, SafeAccount account  = null)
        {
            BitcoinAddress cachedAddress = _safeCache.FirstOrDefault(x => x.Key.Type == type && x.Key.Index == index && x.Key.HdPathType == hdPathType && x.Key.Account == account).Value;
            if (cachedAddress != null) return cachedAddress;

            PubKey pubKey = GetPubKey(index, hdPathType, account);
            BitcoinAddress address = null;
            if (type == AddressType.Pay2WitnessPublicKeyHash)
            {
                address= pubKey.WitHash.ScriptPubKey.GetDestinationAddress(Network);
            }
            else if (type == AddressType.Pay2PublicKeyHash)
            {
                address = pubKey.Hash.ScriptPubKey.GetDestinationAddress(Network);
            }
            else if (type == AddressType.Pay2ScriptHashOverPay2WitnessPublicKeyHash)
            {
                address = pubKey.WitHash.ScriptPubKey.Hash.ScriptPubKey.GetDestinationAddress(Network);
            }
            else throw new NotSupportedException(type.ToString());
            
            _safeCache.TryAdd((type, index, hdPathType, account), address);
            return address;
        }

        public IList<BitcoinAddress> GetFirstNAddresses(AddressType type, int addressCount, HdPathType hdPathType = HdPathType.Receive, SafeAccount account = null)
		{
			var addresses = new List<BitcoinAddress>();

            for (var i = 0; i < addressCount; i++)
            {
                addresses.Add(GetAddress(type, i, hdPathType, account));
            }

            return addresses;
		}

		// Let's generate a unique id from seedpublickey
		// Let's get the pubkey, so the chaincode is lost
		// Let's get the address, you can't directly access it from the safe
		// Also nobody would ever use this address for anythin
		/// <summary> If the wallet only differs by CreationTime, the UniqueId will be the same </summary>
		public string UniqueId => BitcoinExtKey.Neuter().ExtPubKey.PubKey.GetAddress(Network).ToString();
		
		public string WalletFilePath { get; }

		protected Safe(string password, string walletFilePath, Network network, DateTimeOffset creationTime, Mnemonic mnemonic = null)
		{
			Network = network;
			WalletFilePath = walletFilePath;
			CreationTime = creationTime > EarliestPossibleCreationTime ? creationTime : EarliestPossibleCreationTime;

			if (mnemonic != null)
			{
				SetSeed(password, mnemonic);
			}

		}

		public Safe(Safe safe)
		{
			Network = safe.Network;
			CreationTime = safe.CreationTime;
			ExtKey = safe.ExtKey;
			WalletFilePath = safe.WalletFilePath;
		}

		/// <summary>
		///     Creates a mnemonic, a seed, encrypts it and stores in the specified path.
		/// </summary>
		/// <param name="mnemonic">empty</param>
		/// <param name="password"></param>
		/// <param name="walletFilePath"></param>
		/// <param name="network"></param>
		/// <returns>Safe</returns>
		public static async Task<(Safe Safe, Mnemonic Mnemonic)> CreateAsync(string password, string walletFilePath, Network network)
		{
			var creationTime = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);

			var safe = new Safe(password, walletFilePath, network, creationTime);

			var mnemonic = safe.SetSeed(password);

			await safe.SaveAsync(password, walletFilePath, network, creationTime);

			return (safe, mnemonic);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="mnemonic"></param>
		/// <param name="password"></param>
		/// <param name="walletFilePath"></param>
		/// <param name="network"></param>
		/// <param name="creationTime">if null then will default to EarliestPossibleCreationTime</param>
		/// <returns></returns>
		public static async Task<Safe> RecoverAsync(Mnemonic mnemonic, string password, string walletFilePath, Network network, DateTimeOffset? creationTime = null)
		{
			if(creationTime == null)
				creationTime = EarliestPossibleCreationTime;

			var safe = new Safe(password, walletFilePath, network, (DateTimeOffset)creationTime, mnemonic);
			await safe.SaveAsync(password, walletFilePath, network, safe.CreationTime);
			return safe;
		}

		private Mnemonic SetSeed(string password, Mnemonic mnemonic = null)
		{
			mnemonic = mnemonic ?? new Mnemonic(Wordlist.English, WordCount.Twelve);

			ExtKey = mnemonic.DeriveExtKey(password);

			return mnemonic;
		}

		private void SetSeed(ExtKey seedExtKey) => ExtKey = seedExtKey;

		private async Task SaveAsync(string password, string walletFilePath, Network network, DateTimeOffset creationTime)
		{
			if (File.Exists(walletFilePath))
				throw new NotSupportedException($"Wallet already exists at {walletFilePath}");

			var directoryPath = Path.GetDirectoryName(Path.GetFullPath(walletFilePath));
			if (directoryPath != null) Directory.CreateDirectory(directoryPath);

			var privateKey = ExtKey.PrivateKey;
			var chainCode = ExtKey.ChainCode;

			var encryptedBitcoinPrivateKeyString = privateKey.GetEncryptedBitcoinSecret(password, Network).ToWif();
			var chainCodeString = Convert.ToBase64String(chainCode);

			var networkString = network.ToString();

			var creationTimeString = creationTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

			await WalletFileSerializer.SerializeAsync(
				walletFilePath,
				encryptedBitcoinPrivateKeyString,
				chainCodeString,
				creationTimeString);
		}

		public static async Task<Safe> LoadAsync(string password, string walletFilePath, Network network)
		{
			if (!File.Exists(walletFilePath))
				throw new ArgumentException($"No wallet file found at {walletFilePath}");

			var walletFileRawContent = await WalletFileSerializer.DeserializeAsync(walletFilePath);

			var encryptedBitcoinPrivateKeyString = walletFileRawContent.EncryptedSeed;
			var chainCodeString = walletFileRawContent.ChainCode;

			var chainCode = Convert.FromBase64String(chainCodeString);

			DateTimeOffset creationTime = DateTimeOffset.ParseExact(walletFileRawContent.CreationTime, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

			var safe = new Safe(password, walletFilePath, network, creationTime);
			
			Key privateKey = null;
			foreach (var n in Network.GetNetworks())
			{				
				try
				{
					privateKey = Key.Parse(encryptedBitcoinPrivateKeyString, password, n);
					break;
				}
				catch
				{

				}
			}
			// if private key still null throw the 
			privateKey = privateKey ?? Key.Parse(encryptedBitcoinPrivateKeyString, password, safe.Network);

			var seedExtKey = new ExtKey(privateKey, chainCode);
			safe.SetSeed(seedExtKey);

			return safe;
		}

		public BitcoinExtKey FindPrivateKey(BitcoinAddress address, int stopSearchAfterIteration = 100000, SafeAccount account = null)
		{
            foreach (AddressType type in Enum.GetValues(typeof(AddressType)))
            {
                for (int i = 0; i < stopSearchAfterIteration; i++)
                {
                    if (GetAddress(type, i, HdPathType.Receive, account) == address)
                        return GetBitcoinExtKey(i, HdPathType.Receive, account);
                    if (GetAddress(type, i, HdPathType.Change, account) == address)
                        return GetBitcoinExtKey(i, HdPathType.Change, account);
                    if (GetAddress(type, i, HdPathType.NonHardened, account) == address)
                        return GetBitcoinExtKey(i, HdPathType.NonHardened, account);
                }
            }

            throw new KeyNotFoundException(address.ToString());
		}

		public BitcoinExtKey GetBitcoinExtKey(int? index = null, HdPathType hdPathType = HdPathType.Receive, SafeAccount account = null)
		{
			string firstPart = "";
			if(account != null)
			{
				firstPart += Hierarchy.GetPathString(account) + "/";
			}

			firstPart += Hierarchy.GetPathString(hdPathType);
			string lastPart;
			if (index == null)
			{
				lastPart = "";
			}
			else if (hdPathType == HdPathType.NonHardened)
			{
				lastPart = $"/{index}";
			}
			else
			{
				lastPart = $"/{index}'";
			}

			KeyPath keyPath = new KeyPath(firstPart + lastPart);

			return ExtKey.Derive(keyPath).GetWif(Network);
		}

		public string GetCreationTimeString()
		{
			return CreationTime.ToString("s", CultureInfo.InvariantCulture);
		}

		public void Delete()
		{
			if(File.Exists(WalletFilePath))
				File.Delete(WalletFilePath);
		}
	}
}
