using System;
using System.IO;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace HiddenWallet.KeyManagement
{
	internal class WalletFileSerializer
	{
		[JsonConstructor]
		private WalletFileSerializer(string encryptedBitcoinPrivateKey, string chainCode, string creationTime)
		{
			EncryptedSeed = encryptedBitcoinPrivateKey;
			ChainCode = chainCode;
			CreationTime = creationTime;
		}

		// KEEP THEM PUBLIC OTHERWISE IT WILL NOT SERIALIZE!
		public string EncryptedSeed { get; set; }
		public string ChainCode { get; set; }
		public string CreationTime { get; set; }

		internal static async Task SerializeAsync(string walletFilePath, string encryptedBitcoinPrivateKey, string chainCode, string creationTime)
		{
			var content =
				JsonConvert.SerializeObject(new WalletFileSerializer(encryptedBitcoinPrivateKey, chainCode, creationTime), Formatting.Indented);

			if (File.Exists(walletFilePath))
				throw new NotSupportedException($"Wallet file already exists at {walletFilePath}");

			var directoryPath = Path.GetDirectoryName(Path.GetFullPath(walletFilePath));
			if (directoryPath != null) Directory.CreateDirectory(directoryPath);

			await File.WriteAllTextAsync(walletFilePath, content);
		}

		internal static async Task<WalletFileSerializer> DeserializeAsync(string path)
		{
			if (!File.Exists(path))
				throw new FileNotFoundException($"Wallet not found at {path}");

			var contentString = await File.ReadAllTextAsync(path);
			var walletFileSerializer = JsonConvert.DeserializeObject<WalletFileSerializer>(contentString);

			return new WalletFileSerializer(walletFileSerializer.EncryptedSeed, walletFileSerializer.ChainCode, walletFileSerializer.CreationTime);
		}
	}
}
