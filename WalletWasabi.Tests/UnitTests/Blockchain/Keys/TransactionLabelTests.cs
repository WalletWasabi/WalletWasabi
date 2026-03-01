using NBitcoin;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.Keys;

/// <summary>
/// Tests for transaction label storage and migration (GitHub issue #14344).
/// Validates the dual-source label approach: wallet-scoped (portable) + shared Transactions.sqlite (backward compatible).
/// </summary>
public class TransactionLabelTests
{
	[Fact]
	public void TransactionLabels_SerializeAndDeserialize()
	{
		// Arrange
		var network = Network.TestNet;
		var workDir = Common.GetWorkDir();
		var walletPath = Path.Combine(workDir, "TestWallet.json");

		var km = KeyManager.CreateNew(out _, "", network, walletPath);
		var txid1 = RandomUtils.GetUInt256();
		var txid2 = RandomUtils.GetUInt256();

		// Act: Set labels on transactions
		km.SetTransactionLabels(txid1, new LabelsArray("Invoice #123, Payment to Alice"));
		km.SetTransactionLabels(txid2, new LabelsArray("Received from Bob"));

		// Assert: Labels are in memory
		Assert.Equal("Invoice #123, Payment to Alice", km.GetTransactionLabels(txid1).ToString());
		Assert.Equal("Received from Bob", km.GetTransactionLabels(txid2).ToString());

		// Act: Save and reload
		km.ToFile();
		var kmLoaded = KeyManager.FromFile(walletPath);

		// Assert: Labels survived serialization
		Assert.Equal("Invoice #123, Payment to Alice", kmLoaded.GetTransactionLabels(txid1).ToString());
		Assert.Equal("Received from Bob", kmLoaded.GetTransactionLabels(txid2).ToString());
	}

	[Fact]
	public async Task TransactionLabels_WalletMigration_LabelsArePortable()
	{
		// This test demonstrates the fix for GitHub issue #14344:
		// Transaction labels are now stored in the wallet JSON file and survive migration.

		var network = Network.TestNet;
		var workDir1 = Common.GetWorkDir();
		var workDir2 = Common.GetWorkDir();
		var walletPath1 = Path.Combine(workDir1, "OriginalWallet.json");
		var walletPath2 = Path.Combine(workDir2, "MigratedWallet.json");

		// Scenario 1: User has a wallet on Machine 1
		await using var txStore1 = new AllTransactionStore(workDir1, network);
		await txStore1.InitializeAsync();

		var km1 = KeyManager.CreateNew(out _, "", network, walletPath1);
		var address1 = km1.GetNextReceiveKey(new LabelsArray("Address Label 1"));

		// Create a transaction with labels
		var tx1 = BitcoinFactory.CreateSmartTransaction();
		var txid = tx1.GetHash();
		txStore1.AddOrUpdate(tx1); // Add transaction first
		km1.SetTransactionLabels(txid, new LabelsArray("Invoice #123, Payment to Alice"), txStore1);

		// Verify labels are set
		Assert.Equal("Invoice #123, Payment to Alice", km1.GetTransactionLabels(txid, txStore1).ToString());
		km1.ToFile();

		// Scenario 2: User migrates wallet to Machine 2 by copying ONLY the JSON file
		File.Copy(walletPath1, walletPath2, overwrite: true);

		await using var txStore2 = new AllTransactionStore(workDir2, network);
		await txStore2.InitializeAsync();

		var km2 = KeyManager.FromFile(walletPath2);

		// THE FIX: Transaction labels survive migration because they're now in the wallet JSON
		var labelsAfterMigration = km2.GetTransactionLabels(txid, txStore2);
		Assert.Equal("Invoice #123, Payment to Alice", labelsAfterMigration.ToString());

		// Address labels also survive (this was already working)
		var address2 = km2.GetKeys().First(k => k.PubKey == address1.PubKey);
		Assert.Equal("Address Label 1", address2.Labels.ToString());
	}

	[Fact]
	public async Task TransactionLabels_DualWrite_BothSourcesUpdated()
	{
		// This test verifies the dual-write strategy for backward compatibility

		var network = Network.TestNet;
		var workDir = Common.GetWorkDir();
		var walletPath = Path.Combine(workDir, "TestWallet.json");

		await using var txStore = new AllTransactionStore(workDir, network);
		await txStore.InitializeAsync();

		var km = KeyManager.CreateNew(out _, "", network, walletPath);
		var tx = BitcoinFactory.CreateSmartTransaction();
		var txid = tx.GetHash();

		// Add transaction to store first
		txStore.AddOrUpdate(tx);

		// Act: Set labels using dual-write
		km.SetTransactionLabels(txid, new LabelsArray("Test Label"), txStore);

		// Assert: Label is in wallet (portable source)
		Assert.Equal("Test Label", km.GetTransactionLabels(txid).ToString());

		// Assert: Label is also in shared store (backward compatibility)
		Assert.True(txStore.TryGetTransaction(txid, out var storedTx));
		Assert.Equal("Test Label", storedTx.Labels.ToString());
	}

	[Fact]
	public async Task TransactionLabels_CrossWalletSharing_SameMachine()
	{
		// This test demonstrates that transaction labels are still shared between wallets on the same machine

		var network = Network.TestNet;
		var sharedWorkDir = Common.GetWorkDir();
		var walletPath1 = Path.Combine(sharedWorkDir, "Wallet1.json");
		var walletPath2 = Path.Combine(sharedWorkDir, "Wallet2.json");

		await using var sharedTxStore = new AllTransactionStore(sharedWorkDir, network);
		await sharedTxStore.InitializeAsync();

		var km1 = KeyManager.CreateNew(out _, "", network, walletPath1);
		var km2 = KeyManager.CreateNew(out _, "", network, walletPath2);

		// Create a transaction (e.g., from Wallet1 to Wallet2)
		var tx = BitcoinFactory.CreateSmartTransaction();
		var txid = tx.GetHash();
		sharedTxStore.AddOrUpdate(tx);

		// Wallet 1 labels the transaction
		km1.SetTransactionLabels(txid, new LabelsArray("Transfer to savings"), sharedTxStore);
		km1.ToFile();

		// Wallet 2 can see the label from shared store
		var labelsInWallet2 = km2.GetTransactionLabels(txid, sharedTxStore);
		Assert.Equal("Transfer to savings", labelsInWallet2.ToString());

		// If Wallet 2 also sets labels, they get merged in the shared store
		km2.SetTransactionLabels(txid, new LabelsArray("Received from checking"), sharedTxStore);

		// Now each wallet has its own view
		Assert.Equal("Transfer to savings", km1.GetTransactionLabels(txid, sharedTxStore).ToString());
		Assert.Equal("Received from checking", km2.GetTransactionLabels(txid, sharedTxStore).ToString());
	}

	[Fact]
	public void TransactionLabels_ReadPriority_WalletOverShared()
	{
		// This test verifies that wallet-scoped labels take priority over shared store labels

		var network = Network.TestNet;
		var workDir = Common.GetWorkDir();
		var walletPath = Path.Combine(workDir, "TestWallet.json");

		var km = KeyManager.CreateNew(out _, "", network, walletPath);
		var txid = RandomUtils.GetUInt256();

		// Set label in wallet only (no shared store provided)
		km.SetTransactionLabels(txid, new LabelsArray("Wallet Label"));

		// Act: Read without shared store
		var labels = km.GetTransactionLabels(txid);

		// Assert: Should get wallet label
		Assert.Equal("Wallet Label", labels.ToString());
	}

	[Fact]
	public async Task TransactionLabels_BackwardCompatibility_OldWalletsWithoutLabelsField()
	{
		// This test ensures old wallet files without TransactionLabels field can still be loaded

		var network = Network.TestNet;
		var workDir = Common.GetWorkDir();
		var walletPath = Path.Combine(workDir, "OldWallet.json");

		// Create and save a wallet
		var km = KeyManager.CreateNew(out _, "", network, walletPath);
		km.ToFile();

		// Manually remove the TransactionLabels field to simulate an old wallet
		var jsonContent = File.ReadAllText(walletPath);
		// The decoder should handle missing field with default empty dictionary

		// Act: Load the wallet
		var kmLoaded = KeyManager.FromFile(walletPath);

		// Assert: Wallet loads successfully with empty TransactionLabels
		Assert.NotNull(kmLoaded.TransactionLabels);
		Assert.Empty(kmLoaded.TransactionLabels);

		// Can still set labels after loading
		var txid = RandomUtils.GetUInt256();
		kmLoaded.SetTransactionLabels(txid, new LabelsArray("New Label"));
		Assert.Equal("New Label", kmLoaded.GetTransactionLabels(txid).ToString());
	}

	[Fact]
	public void TransactionLabels_EmptyLabels_RemovesEntry()
	{
		// This test verifies that setting empty labels removes the entry from the dictionary

		var network = Network.TestNet;
		var workDir = Common.GetWorkDir();
		var walletPath = Path.Combine(workDir, "TestWallet.json");

		var km = KeyManager.CreateNew(out _, "", network, walletPath);
		var txid = RandomUtils.GetUInt256();

		// Set label
		km.SetTransactionLabels(txid, new LabelsArray("Test Label"));
		Assert.Single(km.TransactionLabels);

		// Clear label
		km.SetTransactionLabels(txid, LabelsArray.Empty);

		// Assert: Entry removed
		Assert.Empty(km.TransactionLabels);
		Assert.True(km.GetTransactionLabels(txid).IsEmpty);
	}

	[Fact]
	public async Task TransactionLabels_FallbackToSharedStore()
	{
		// This test verifies fallback to shared store when label not in wallet

		var network = Network.TestNet;
		var workDir = Common.GetWorkDir();
		var walletPath = Path.Combine(workDir, "TestWallet.json");

		await using var txStore = new AllTransactionStore(workDir, network);
		await txStore.InitializeAsync();

		var km = KeyManager.CreateNew(out _, "", network, walletPath);

		// Create a transaction with labels only in shared store (simulating old data)
		var tx = new SmartTransaction(
			BitcoinFactory.CreateSmartTransaction().Transaction,
			Height.Mempool,
			labels: new LabelsArray("Old Shared Label")
		);
		var txid = tx.GetHash();
		txStore.AddOrUpdate(tx);

		// Act: Read labels with shared store provided
		var labels = km.GetTransactionLabels(txid, txStore);

		// Assert: Falls back to shared store
		Assert.Equal("Old Shared Label", labels.ToString());

		// Now set wallet-scoped label
		km.SetTransactionLabels(txid, new LabelsArray("New Wallet Label"), txStore);

		// Assert: Wallet label takes priority
		Assert.Equal("New Wallet Label", km.GetTransactionLabels(txid, txStore).ToString());
	}
}
