using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Mobile;
using Xunit;

namespace WalletWasabi.Mobile.Tests;

[Collection("Wallet sessions")]
public class WalletSessionTests
{
	private const string Words = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
	private const string Password = "public test passphrase";

	[Fact]
	public async Task PersistRecoverAndLockSigningKeys()
	{
		var path = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
		await using var session = new WalletSession(path, new MobileSettings { Network = "regtest" }, path);
		var wallet = await session.CreateAsync("Phone wallet", Password, new Mnemonic(Words), false);
		var address = session.Receive("test");
		Assert.StartsWith("bcrt1p", address);
		Assert.NotEqual(session.Receive(""), session.Receive(""));
		Assert.True(session.IsUnlocked);
		Assert.Null(wallet.KeyChain);
		Assert.Empty(wallet.Password);
		var cachedMaster = wallet.KeyManager.GetMasterExtKey(Password);
		var file = File.ReadAllText(wallet.KeyManager.FilePath!);
		Assert.DoesNotContain(Words, file);
		Assert.DoesNotContain(Password, file);
		var reopened = KeyManager.FromFile(wallet.KeyManager.FilePath!);
		Assert.Equal(wallet.KeyManager.SegwitExtPubKey, reopened.SegwitExtPubKey);
		Assert.Equal(wallet.KeyManager.TaprootExtPubKey, reopened.TaprootExtPubKey);
		session.Lock();
		Assert.False(wallet.IsLoggedIn);
		Assert.Equal("", wallet.Password);
		Assert.Null(wallet.KeyChain);
		Assert.Throws<InvalidOperationException>(() => session.Receive("locked"));
		session.Unlock(wallet, Password);
		Assert.NotSame(cachedMaster, wallet.KeyManager.GetMasterExtKey(Password));
		Assert.Throws<UnauthorizedAccessException>(() => session.Unlock(wallet, "wrong password"));
		var recovered = await session.CreateAsync("Recovery wallet", Password, new Mnemonic(Words), true);
		Assert.Equal(wallet.KeyManager.SegwitExtPubKey, recovered.KeyManager.SegwitExtPubKey);
		Assert.Equal(wallet.KeyManager.TaprootExtPubKey, recovered.KeyManager.TaprootExtPubKey);
		Assert.Equal(0u, (uint)recovered.KeyManager.GetBestHeight());
		Assert.Throws<ArgumentException>(() => WalletGenerator.GetWalletFilePath("../escape", path));
	}

	[Fact]
	public async Task MainnetRecoveryStartsAtTheEarliestSupportedCheckpoint()
	{
		var path = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
		await using var session = new WalletSession(path, new MobileSettings { Network = "main" }, path, WalletPolicy.Personal);
		var wallet = await session.CreateAsync("Recovered", Password, new Mnemonic(Words), true);
		Assert.Equal(FilterCheckpoints.GetWasabiGenesisFilter(Network.Main).Header.Height, wallet.KeyManager.GetBestHeight());
		Assert.Equal(wallet.KeyManager.GetBestHeight(), wallet.KeyManager.GetBirthHeight());
	}

	[Fact]
	public async Task ImportPreservesKeysAndRejectsWrongNetworksAndNames()
	{
		var path = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
		await using var session = new WalletSession(path, new MobileSettings { Network = "regtest" }, path);
		var original = await session.CreateAsync("Original", Password, new Mnemonic(Words), false);
		original.KeyManager.SetBestHeight((WalletWasabi.Models.Height.ChainHeight)101u);
		var json = File.ReadAllText(original.KeyManager.FilePath!);
		var imported = await session.ImportAsync("Imported", json);
		Assert.Equal(original.KeyManager.SegwitExtPubKey, imported.KeyManager.SegwitExtPubKey);
		Assert.False(imported.IsLoggedIn);
		Assert.Equal(0u, (uint)imported.KeyManager.GetBestHeight());
		session.Unlock(imported, Password);
		Assert.True(session.IsUnlocked);
		Assert.Null(imported.KeyChain);
		await Assert.ThrowsAsync<ArgumentException>(() => session.ImportAsync("Imported", json));
		await Assert.ThrowsAsync<ArgumentException>(() => session.ImportAsync("../outside", json));
		await Assert.ThrowsAsync<FormatException>(() => session.ImportAsync("Oversize", new string('x', 4 * 1024 * 1024 + 1)));
		var mainnet = KeyManager.CreateNew(new Mnemonic(Words), Password, Network.Main, Path.Combine(path, "main.json"));
		mainnet.ToFile();
		await Assert.ThrowsAsync<FormatException>(() => session.ImportAsync("Wrong network", File.ReadAllText(mainnet.FilePath!)));
		Assert.Equal(2, session.Global.WalletManager.GetWallets().Count());
	}

	[Fact]
	public async Task NeverSendFromAnUnsynchronizedWallet()
	{
		var path = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
		await using var session = new WalletSession(path, new MobileSettings { Network = "regtest" }, path);
		await session.CreateAsync("Offline", Password, new Mnemonic(Words), false);
		var request = PaymentRequest.Parse(session.Receive("test"), Network.RegTest);
		await Assert.ThrowsAsync<InvalidOperationException>(() => session.PrepareAsync(request, Money.Coins(1), new FeeRate(1m), null));
		await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartCoinJoinAsync(Password));
	}

	[Fact]
	public async Task NetworkSettingsKeepWalletsSeparate()
	{
		var path = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
		await using (var main = new WalletSession(path, new MobileSettings { Network = "main" }, path, WalletPolicy.Personal))
		{
			await main.CreateAsync("Same name", Password, new Mnemonic(Words), false);
			Assert.StartsWith("bc1", main.Receive("test"));
		}
		await using var test = new WalletSession(path, new MobileSettings { Network = "testnet" }, path);
		await test.CreateAsync("Same name", Password, new Mnemonic(Words), false);
		Assert.Contains("TestNet4", test.Current!.KeyManager.FilePath!);
		Assert.StartsWith("tb1", test.Receive("test"));
	}

	[Theory]
	[InlineData("http://example.com")]
	[InlineData("https://user:secret@example.com")]
	[InlineData("https://example.com/#fragment")]
	[InlineData("file:///wallet")]
	public void RejectInsecureCoordinatorConfiguration(string coordinator) => Assert.Throws<FormatException>(() => new MobileSettings { Coordinator = coordinator }.Validate());

	[Theory]
	[InlineData("http://example.com")]
	[InlineData("https://example.com")]
	[InlineData("http://user:password@localhost")]
	[InlineData("file:///wallet")]
	public void PersonalNodeCannotExposeRpcCredentialsOutsideTor(string uri) => Assert.Throws<FormatException>(() => new MobileSettings { BitcoinRpcUri = uri }.Validate());

	[Fact]
	public void PersonalNodeSettingsRoundTrip()
	{
		var path = Path.Combine(Path.GetTempPath(), "wasabi-mobile-tests", Guid.NewGuid().ToString("N"));
		var settings = new MobileSettings { BitcoinRpcUri = "http://127.0.0.1:18443", BitcoinRpcCredentials = "public:test" };
		settings.Save(path);
		Assert.Equal(settings with { BitcoinRpcCredentials = "" }, MobileSettings.Load(path));
		Assert.DoesNotContain("public:test", File.ReadAllText(Path.Combine(path, "mobile-settings.json")));
		Assert.Throws<FormatException>(() => (settings with { Network = null! }).Validate());
	}
}
