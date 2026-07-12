using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions;

/// <summary>
/// Deterministic repro for https://github.com/WalletWasabi/WalletWasabi/issues/14344 :
/// after moving a wallet to another machine (i.e. copying only Client/Wallets/&lt;wallet&gt;.json),
/// the labels of *outgoing* transactions are gone, while the labels of *incoming* ones survive.
/// </summary>
public class TransactionLabelMigrationTests
{
	[Fact]
	public async Task OutgoingLabelsAreLostWhenOnlyTheWalletFileIsMigratedAsync()
	{
		var network = Network.RegTest;
		var workDir = Path.Combine(Path.GetTempPath(), $"wasabi-labels-{Guid.NewGuid():N}");
		Directory.CreateDirectory(workDir);
		var walletFilePath = Path.Combine(workDir, "MyWallet.json");

		// ---------------- OLD MACHINE ----------------
		var keyManager = KeyManager.CreateNew(out _, "", network, walletFilePath);
		keyManager.ToFile();

		using var oldStore = new AllTransactionStore(SqliteStorageHelper.InMemoryDatabase, network);
		await oldStore.InitializeAsync();
		var oldProcessor = CreateProcessor(oldStore, keyManager);

		// 1. Receive coins on an address labeled "Alice".
		var receiveKey = keyManager.GetNextReceiveKey(new LabelsArray("Alice"));
		var incomingTx = CreateCreditingTransaction(network, receiveKey.P2wpkhScript, Money.Coins(1m), height: 100);
		oldProcessor.Process(incomingTx);

		// 2. Send coins out, labeling the payment "Bob" -- exactly what the Send dialog does.
		var coin = oldProcessor.Coins.Single();
		var factory = new TransactionFactory(network, keyManager, oldProcessor.Coins, oldStore, "");
		using var destinationKey = new Key();
		var payment = new PaymentIntent(destinationKey, Money.Coins(0.5m), label: new LabelsArray("Bob"));
		var parameters = TransactionParametersBuilder.CreateDefault()
			.SetPayment(payment)
			.SetFeeRate(2m)
			.SetAllowUnconfirmed(true)
			.Build();
		var buildResult = factory.BuildTransaction(parameters);

		var outgoingTx = buildResult.Transaction;
		oldProcessor.Process(outgoingTx);

		// This is what the UI does after a successful broadcast (Wallet.UpdateUsedHdPubKeysLabels).
		foreach (var (hdPubKey, labels) in buildResult.HdPubKeysWithNewLabels)
		{
			hdPubKey.SetLabel(labels);
		}
		keyManager.ToFile();

		var incomingTxId = incomingTx.GetHash();
		var outgoingTxId = outgoingTx.GetHash();

		// On the old machine both labels are shown.
		Assert.Equal("Alice", GetStoredLabels(oldStore, incomingTxId));
		Assert.Equal("Bob", GetStoredLabels(oldStore, outgoingTxId));

		// ---------------- NEW MACHINE ----------------
		// The user copies ONLY Client/Wallets/MyWallet.json, and Wasabi rescans the chain from
		// scratch: transactions come from blocks, so they arrive without any label.
		var migratedKeyManager = KeyManager.FromFile(walletFilePath);

		using var newStore = new AllTransactionStore(SqliteStorageHelper.InMemoryDatabase, network);
		await newStore.InitializeAsync();
		var newProcessor = CreateProcessor(newStore, migratedKeyManager);

		newProcessor.Process(FromBlockchain(incomingTx));
		newProcessor.Process(FromBlockchain(outgoingTx));

		// The incoming label is rebuilt from HdPubKeys[].Label in the wallet file.
		Assert.Equal("Alice", GetStoredLabels(newStore, incomingTxId));

		// Before the fix the outgoing one was gone: it only ever lived in the shared
		// Transactions.sqlite, which is not part of the wallet backup. This is #14344.
		Assert.Equal("Bob", GetStoredLabels(newStore, outgoingTxId));
	}

	/// <summary>
	/// A wallet that was created before the labels of sent transactions were kept in the wallet file has
	/// them in the transaction store only. Replaying the history -- which <c>Wallet.LoadWalletStateAsync</c>
	/// does on every load -- has to bring them into the wallet file, or the very next migration loses them.
	/// </summary>
	[Fact]
	public async Task LegacyLabelsAreAdoptedFromTheTransactionStoreAsync()
	{
		var network = Network.RegTest;
		var workDir = Path.Combine(Path.GetTempPath(), $"wasabi-labels-{Guid.NewGuid():N}");
		Directory.CreateDirectory(workDir);
		var walletFilePath = Path.Combine(workDir, "MyWallet.json");

		var keyManager = KeyManager.CreateNew(out _, "", network, walletFilePath);
		using var store = new AllTransactionStore(SqliteStorageHelper.InMemoryDatabase, network);
		await store.InitializeAsync();

		var receiveKey = keyManager.GetNextReceiveKey(new LabelsArray("Alice"));
		var incomingTx = CreateCreditingTransaction(network, receiveKey.P2wpkhScript, Money.Coins(1m), height: 100);
		var processor = CreateProcessor(store, keyManager);
		processor.Process(incomingTx);

		var factory = new TransactionFactory(network, keyManager, processor.Coins, store, "");
		using var destinationKey = new Key();
		var parameters = TransactionParametersBuilder.CreateDefault()
			.SetPayment(new PaymentIntent(destinationKey, Money.Coins(0.5m), label: new LabelsArray("Bob")))
			.SetFeeRate(2m)
			.SetAllowUnconfirmed(true)
			.Build();
		var outgoingTx = factory.BuildTransaction(parameters).Transaction;
		processor.Process(outgoingTx);
		keyManager.ToFile();

		// Rewrite the wallet file as a pre-fix Wasabi would have left it: the label of the sent
		// transaction lives in the transaction store, and nowhere else.
		var legacyWalletFilePath = Path.Combine(workDir, "LegacyWallet.json");
		var walletJson = JsonNode.Parse(await File.ReadAllTextAsync(walletFilePath))!.AsObject();
		Assert.True(walletJson.Remove("TransactionLabels"));
		await File.WriteAllTextAsync(legacyWalletFilePath, walletJson.ToJsonString());

		var legacyKeyManager = KeyManager.FromFile(legacyWalletFilePath);
		Assert.True(legacyKeyManager.GetTransactionLabels(outgoingTx.GetHash()).IsEmpty);

		// Loading the wallet replays the stored history, which adopts the label...
		CreateProcessor(store, legacyKeyManager).Process(store.GetTransactions());
		Assert.Equal("Bob", legacyKeyManager.GetTransactionLabels(outgoingTx.GetHash()).ToString());

		// ...and it is on disk from then on.
		Assert.True(legacyKeyManager.HasUnsavedTransactionLabels);
		legacyKeyManager.ToFile();
		Assert.Equal("Bob", KeyManager.FromFile(legacyWalletFilePath).GetTransactionLabels(outgoingTx.GetHash()).ToString());
	}

	/// <summary>
	/// Writing the wallet file rewrites it whole, so the transaction processor -- which walks the entire
	/// history -- must not do it per transaction, or syncing a wallet with a long history grinds to a halt.
	/// </summary>
	[Fact]
	public async Task ProcessingTransactionsDoesNotWriteTheWalletFileAsync()
	{
		var network = Network.RegTest;
		var workDir = Path.Combine(Path.GetTempPath(), $"wasabi-labels-{Guid.NewGuid():N}");
		Directory.CreateDirectory(workDir);
		var walletFilePath = Path.Combine(workDir, "MyWallet.json");

		var keyManager = KeyManager.CreateNew(out _, "", network, walletFilePath);
		using var store = new AllTransactionStore(SqliteStorageHelper.InMemoryDatabase, network);
		await store.InitializeAsync();
		var processor = CreateProcessor(store, keyManager);

		var receiveKey = keyManager.GetNextReceiveKey(new LabelsArray("Alice"));
		processor.Process(CreateCreditingTransaction(network, receiveKey.P2wpkhScript, Money.Coins(1m), height: 100));

		var factory = new TransactionFactory(network, keyManager, processor.Coins, store, "");
		using var destinationKey = new Key();
		var parameters = TransactionParametersBuilder.CreateDefault()
			.SetPayment(new PaymentIntent(destinationKey, Money.Coins(0.5m), label: new LabelsArray("Bob")))
			.SetFeeRate(2m)
			.SetAllowUnconfirmed(true)
			.Build();
		var outgoingTx = factory.BuildTransaction(parameters).Transaction;

		keyManager.ToFile();
		var writeTime = File.GetLastWriteTimeUtc(walletFilePath);

		processor.Process(outgoingTx);

		// The label is remembered...
		Assert.Equal("Bob", keyManager.GetTransactionLabels(outgoingTx.GetHash()).ToString());

		// ...without the processor having touched the disk. The pending write is flushed later, by the
		// ToFile() the filter processor already does for every block this wallet has a transaction in.
		Assert.True(keyManager.HasUnsavedTransactionLabels);
		Assert.Equal(writeTime, File.GetLastWriteTimeUtc(walletFilePath));
		Assert.DoesNotContain("Bob", await File.ReadAllTextAsync(walletFilePath));
	}

	private static TransactionProcessor CreateProcessor(AllTransactionStore store, KeyManager keyManager) =>
		new(store, null, keyManager, Money.Coins(0.0001m), new EventBus());

	private static string GetStoredLabels(AllTransactionStore store, uint256 txid)
	{
		Assert.True(store.TryGetTransaction(txid, out var tx));
		return tx.Labels.ToString();
	}

	/// <summary>A transaction as it comes back from a block during a rescan: no labels attached.</summary>
	private static SmartTransaction FromBlockchain(SmartTransaction tx) =>
		new(tx.Transaction, new Height.ChainHeight.ChainHeight(200));

	private static SmartTransaction CreateCreditingTransaction(Network network, Script scriptPubKey, Money amount, uint height)
	{
		var tx = network.CreateTransaction();
		tx.Version = 1;
		tx.LockTime = LockTime.Zero;
		tx.Inputs.Add(new OutPoint(RandomUtils.GetUInt256(), 0), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
		tx.Outputs.Add(amount, scriptPubKey);
		return new SmartTransaction(tx, new Height.ChainHeight.ChainHeight(height));
	}
}
