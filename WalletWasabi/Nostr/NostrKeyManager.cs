using System.IO;
using NBitcoin;
using NBitcoin.Secp256k1;

namespace WalletWasabi.Nostr;

public class NostrKeyManager : IDisposable
{
	private readonly string _keyFileName = "discovery.key";
	private readonly string _folderName = "Nostr";

	public NostrKeyManager(string dataDir)
	{
		Key = GetOrCreateKey(dataDir);
	}

	public ECPrivKey Key { get; }

	private ECPrivKey GetOrCreateKey(string dataDir)
	{
		var folderPath = Path.Combine(dataDir, _folderName);
		var keyPath = Path.Combine(folderPath, _keyFileName);

		if (!Directory.Exists(folderPath))
		{
			Directory.CreateDirectory(folderPath);
		}

		if (File.Exists(keyPath))
		{
			var keyBytes = File.ReadAllBytes(keyPath);
			return ECPrivKey.Create(keyBytes);
		}
		else
		{
			using var key = new Key();
			var keyBytes = key.ToBytes();
			File.WriteAllBytes(keyPath, keyBytes);
			return ECPrivKey.Create(keyBytes);
		}
	}

	public void Dispose()
	{
		Key.Dispose();
	}
}
