using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions
{
	public class AllTransactionStoreTests
	{
		#region Helpers

		private static SmartTransaction CreateTransaction(int height, uint256 blockHash)
		{
			var tx = Network.RegTest.CreateTransaction();
			tx.Version = 1;
			tx.LockTime = LockTime.Zero;
			tx.Inputs.Add(new OutPoint(RandomUtils.GetUInt256(), 0), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
			tx.Outputs.Add(Money.Coins(1), Script.Empty);
			return new SmartTransaction(tx, new Height(height), blockHash);
		}

		private void PrepareTestEnv(out string dir, out Network network, out string mempoolFile, out string txFile, out SmartTransaction uTx1, out SmartTransaction uTx2, out SmartTransaction uTx3, out SmartTransaction cTx1, out SmartTransaction cTx2, out SmartTransaction cTx3, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
		{
			dir = PrepareWorkDir(EnvironmentHelpers.ExtractFileName(callerFilePath), callerMemberName);
			network = Network.TestNet;
			mempoolFile = Path.Combine(dir, "Mempool", "Transactions.dat");
			txFile = Path.Combine(dir, "ConfirmedTransactions", Constants.ConfirmedTransactionsVersion, "Transactions.dat");
			IoHelpers.EnsureContainingDirectoryExists(mempoolFile);
			IoHelpers.EnsureContainingDirectoryExists(txFile);

			uTx1 = SmartTransaction.FromLine("34fc45781f2ac8e541b6045c2858c755dd2ab85e0ea7b5778b4d0cc191468571:01000000000102d5ae6e2612cdf8932d0e4f684d8ad9afdbca0afffba5c3dc0bf85f2b661bfb670000000000ffffffffbfd117908d5ba536624630519aaea7419605efa33bf1cb50c5ff7441f7b27a5b0100000000ffffffff01c6473d0000000000160014f9d25fe27812c3d35ad3819fcab8b95098effe15024730440220165730f8275434a5484b6aba3c71338a109b7cfd7380fdc18c6791a6afba9dee0220633b3b65313e57bdbd491d17232e6702912869d81461b4c939600d1cc99c06ec012102667c9fb272660cd6c06f853314b53c41da851f86024f9475ff15ea0636f564130247304402205e81562139231274cd7f705772c2810e34b7a291bd9882e6c567553babca9c7402206554ebd60d62a605a27bd4bf6482a533dd8dd35b12dc9d6abfa21738fdf7b57a012102b25dec5439a1aad8d901953a90d16252514a55f547fe2b85bc12e3f72cff1c4b00000000:Mempool::0::1570464578:False", network);
			uTx2 = SmartTransaction.FromLine("b5cd5b4431891d913a6fbc0e946798b6f730c8b97f95a5c730c24189bed24ce7:01000000010113145dd6264da43406a0819b6e2d791ec9281322f33944ef034c3307f38c330000000000ffffffff0220a10700000000001600149af662cf9564700b93bd46fac8b51f64f2adad2343a5070000000000160014f1938902267eac1ae527128fe7773657d2b757b900000000:Mempool::0::1555590391:False", network);
			uTx3 = SmartTransaction.FromLine("89e6788e63c5966fae0ccf6e85665ec62754f982b9593d7e3c4b78ac0e93208d:01000000000101f3e7c1bce1e0566800d8e6cae8f0d771a2ace8939cc6be7c8c21b05e590969530000000000ffffffff01cc2b0f0000000000160014e0ff6f42032bbfda63fabe0832b4ccb7be7350ae024730440220791e34413957c0f8348718d5d767f660657faf241801e74b5b81ac69e8912f60022041f3e9aeca137044565e1a81b6bcca74a88166436e5fa5f0e390448ac18fa5900121034dc07f3c26734591eb97f7e112888c3198d62bc3718106cba5a5688c62485b4500000000:Mempool::0::1555590448:False", network);
			cTx1 = SmartTransaction.FromLine("95374c1037eb5268c8ae6681eb26756459d19754d41b660c251e6f62df586d29:0100000001357852bdf4e75a4ee2afe213463ff8afbed977ea5459a310777322504254ffdf0100000000ffffffff0240420f0000000000160014dc992214c430bf306fe446e9bac1dfc4ad4d3ee368cc100300000000160014005340d370675c47f7a04eb186200dd98c3d463c00000000:1580176:0000000034522ee38f074e1f4330b9c2f20c6a2b9a96de6f474a5f5f8fa76e2b:307::1569940579:False", network);
			cTx2 = SmartTransaction.FromLine("af73b4c173da1bd24063e35a755babfa40728a282d6f56eeef4ce5a81ab26ee7:01000000013c81d2dcb25ad36781d1a6f9faa68f4a8b927f40e0b4e4b6184bb4761ebfc0dd0000000000ffffffff016f052e0000000000160014ae6e31ee9dae103f50979f761c2f9b44661da24f00000000:1580176:0000000034522ee38f074e1f4330b9c2f20c6a2b9a96de6f474a5f5f8fa76e2b:346::1569940633:False", network);
			cTx3 = SmartTransaction.FromLine("ebcef423f6b03ef89dce076b53e624c966381f76e5d8b2b5034d3615ae950b2f:01000000000101296d58df626f1e250c661bd45497d159647526eb8166aec86852eb37104c37950100000000ffffffff01facb100300000000160014d5461e0e7077d62c4cf9c18a4e9ba10efd4930340247304402206d2c5b2b182474531ed07587e44ea22b136a37d5ddbd35aa2d984da7be5f7e5202202abd8435d9856e3d0892dbd54e9c05f2a20d9d5f333247314b925947a480a2eb01210321dd0574c773a35d4a7ebf17bf8f974b5665c0183598f1db53153e74c876768500000000:1580673:0000000017b09a77b815f3df513ff698d1f3b0e8c5e16ac0d6558e2d831f3bf9:130::1570462988:False", network);
		}

		private string PrepareWorkDir([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
		{
			string dir = Path.Combine(Common.GetWorkDir(callerFilePath, callerMemberName));
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}

			return dir;
		}

		public static IEnumerable<object[]> GetDifferentNetworkValues()
		{
			var networks = new List<Network>
			{
				Network.Main,
				Network.TestNet,
				Network.RegTest
			};

			foreach (Network network in networks)
			{
				yield return new object[] { network };
			}
		}

		#endregion Helpers

		[Theory]
		[MemberData(nameof(GetDifferentNetworkValues))]
		public async Task CanInitializeEmptyAsync(Network network)
		{
			var dir = PrepareWorkDir();
			await using var txStore = new AllTransactionStore(dir, network);
			await txStore.InitializeAsync(ensureBackwardsCompatibility: false);

			Assert.NotNull(txStore.ConfirmedStore);
			Assert.NotNull(txStore.MempoolStore);
			Assert.Empty(txStore.GetTransactions());
			Assert.Empty(txStore.GetTransactionHashes());
			Assert.Empty(txStore.MempoolStore.GetTransactions());
			Assert.Empty(txStore.MempoolStore.GetTransactionHashes());
			Assert.Empty(txStore.ConfirmedStore.GetTransactions());
			Assert.Empty(txStore.ConfirmedStore.GetTransactionHashes());

			uint256 txHash = BitcoinFactory.CreateSmartTransaction().GetHash();
			Assert.False(txStore.Contains(txHash));
			Assert.True(txStore.IsEmpty());
			Assert.False(txStore.TryGetTransaction(txHash, out _));

			var mempoolFile = Path.Combine(dir, "Mempool", "Transactions.dat");
			var txFile = Path.Combine(dir, "ConfirmedTransactions", Constants.ConfirmedTransactionsVersion, "Transactions.dat");
			var mempoolContent = await File.ReadAllBytesAsync(mempoolFile);
			var txContent = await File.ReadAllBytesAsync(txFile);

			Assert.True(File.Exists(mempoolFile));
			Assert.True(File.Exists(txFile));
			Assert.Empty(mempoolContent);
			Assert.Empty(txContent);
		}

		[Fact]
		public async Task CanInitializeAsync()
		{
			string dir = PrepareWorkDir();
			var network = Network.TestNet;
			var mempoolFile = Path.Combine(dir, "Mempool", "Transactions.dat");
			var txFile = Path.Combine(dir, "ConfirmedTransactions", Constants.ConfirmedTransactionsVersion, "Transactions.dat");
			IoHelpers.EnsureContainingDirectoryExists(mempoolFile);
			IoHelpers.EnsureContainingDirectoryExists(txFile);

			var uTx1 = SmartTransaction.FromLine("34fc45781f2ac8e541b6045c2858c755dd2ab85e0ea7b5778b4d0cc191468571:01000000000102d5ae6e2612cdf8932d0e4f684d8ad9afdbca0afffba5c3dc0bf85f2b661bfb670000000000ffffffffbfd117908d5ba536624630519aaea7419605efa33bf1cb50c5ff7441f7b27a5b0100000000ffffffff01c6473d0000000000160014f9d25fe27812c3d35ad3819fcab8b95098effe15024730440220165730f8275434a5484b6aba3c71338a109b7cfd7380fdc18c6791a6afba9dee0220633b3b65313e57bdbd491d17232e6702912869d81461b4c939600d1cc99c06ec012102667c9fb272660cd6c06f853314b53c41da851f86024f9475ff15ea0636f564130247304402205e81562139231274cd7f705772c2810e34b7a291bd9882e6c567553babca9c7402206554ebd60d62a605a27bd4bf6482a533dd8dd35b12dc9d6abfa21738fdf7b57a012102b25dec5439a1aad8d901953a90d16252514a55f547fe2b85bc12e3f72cff1c4b00000000:Mempool::0::1570464578:False", network);
			var uTx2 = SmartTransaction.FromLine("b5cd5b4431891d913a6fbc0e946798b6f730c8b97f95a5c730c24189bed24ce7:01000000010113145dd6264da43406a0819b6e2d791ec9281322f33944ef034c3307f38c330000000000ffffffff0220a10700000000001600149af662cf9564700b93bd46fac8b51f64f2adad2343a5070000000000160014f1938902267eac1ae527128fe7773657d2b757b900000000:Mempool::0::1555590391:False", network);
			var uTx3 = SmartTransaction.FromLine("89e6788e63c5966fae0ccf6e85665ec62754f982b9593d7e3c4b78ac0e93208d:01000000000101f3e7c1bce1e0566800d8e6cae8f0d771a2ace8939cc6be7c8c21b05e590969530000000000ffffffff01cc2b0f0000000000160014e0ff6f42032bbfda63fabe0832b4ccb7be7350ae024730440220791e34413957c0f8348718d5d767f660657faf241801e74b5b81ac69e8912f60022041f3e9aeca137044565e1a81b6bcca74a88166436e5fa5f0e390448ac18fa5900121034dc07f3c26734591eb97f7e112888c3198d62bc3718106cba5a5688c62485b4500000000:Mempool::0::1555590448:False", network);
			var cTx1 = SmartTransaction.FromLine("95374c1037eb5268c8ae6681eb26756459d19754d41b660c251e6f62df586d29:0100000001357852bdf4e75a4ee2afe213463ff8afbed977ea5459a310777322504254ffdf0100000000ffffffff0240420f0000000000160014dc992214c430bf306fe446e9bac1dfc4ad4d3ee368cc100300000000160014005340d370675c47f7a04eb186200dd98c3d463c00000000:1580176:0000000034522ee38f074e1f4330b9c2f20c6a2b9a96de6f474a5f5f8fa76e2b:307::1569940579:False", network);
			var cTx2 = SmartTransaction.FromLine("af73b4c173da1bd24063e35a755babfa40728a282d6f56eeef4ce5a81ab26ee7:01000000013c81d2dcb25ad36781d1a6f9faa68f4a8b927f40e0b4e4b6184bb4761ebfc0dd0000000000ffffffff016f052e0000000000160014ae6e31ee9dae103f50979f761c2f9b44661da24f00000000:1580176:0000000034522ee38f074e1f4330b9c2f20c6a2b9a96de6f474a5f5f8fa76e2b:346::1569940633:False", network);
			var cTx3 = SmartTransaction.FromLine("ebcef423f6b03ef89dce076b53e624c966381f76e5d8b2b5034d3615ae950b2f:01000000000101296d58df626f1e250c661bd45497d159647526eb8166aec86852eb37104c37950100000000ffffffff01facb100300000000160014d5461e0e7077d62c4cf9c18a4e9ba10efd4930340247304402206d2c5b2b182474531ed07587e44ea22b136a37d5ddbd35aa2d984da7be5f7e5202202abd8435d9856e3d0892dbd54e9c05f2a20d9d5f333247314b925947a480a2eb01210321dd0574c773a35d4a7ebf17bf8f974b5665c0183598f1db53153e74c876768500000000:1580673:0000000017b09a77b815f3df513ff698d1f3b0e8c5e16ac0d6558e2d831f3bf9:130::1570462988:False", network);

			var mempoolFileContent = new[]
			{
				uTx1.ToLine(),
				uTx2.ToLine(),
				uTx3.ToLine()
			};
			var txFileContent = new[]
			{
				cTx1.ToLine(),
				cTx2.ToLine(),
				cTx3.ToLine()
			};
			await File.WriteAllLinesAsync(mempoolFile, mempoolFileContent);
			await File.WriteAllLinesAsync(txFile, txFileContent);

			await using var txStore = new AllTransactionStore(dir, network);
			await txStore.InitializeAsync(ensureBackwardsCompatibility: false);

			Assert.Equal(6, txStore.GetTransactions().Count());
			Assert.Equal(6, txStore.GetTransactionHashes().Count());
			Assert.Equal(3, txStore.MempoolStore.GetTransactions().Count());
			Assert.Equal(3, txStore.MempoolStore.GetTransactionHashes().Count());
			Assert.Equal(3, txStore.ConfirmedStore.GetTransactions().Count());
			Assert.Equal(3, txStore.ConfirmedStore.GetTransactionHashes().Count());
			Assert.False(txStore.IsEmpty());

			uint256 doesntContainTxHash = BitcoinFactory.CreateSmartTransaction().GetHash();
			Assert.False(txStore.Contains(doesntContainTxHash));
			Assert.False(txStore.TryGetTransaction(doesntContainTxHash, out _));

			Assert.True(txStore.Contains(uTx1.GetHash()));
			Assert.True(txStore.Contains(uTx2.GetHash()));
			Assert.True(txStore.Contains(uTx3.GetHash()));
			Assert.True(txStore.Contains(cTx2.GetHash()));
			Assert.True(txStore.Contains(cTx2.GetHash()));
			Assert.True(txStore.Contains(cTx3.GetHash()));
			Assert.True(txStore.TryGetTransaction(uTx1.GetHash(), out var uTx1Same));
			Assert.True(txStore.TryGetTransaction(uTx2.GetHash(), out var uTx2Same));
			Assert.True(txStore.TryGetTransaction(uTx3.GetHash(), out var uTx3Same));
			Assert.True(txStore.TryGetTransaction(cTx1.GetHash(), out var cTx1Same));
			Assert.True(txStore.TryGetTransaction(cTx2.GetHash(), out var cTx2Same));
			Assert.True(txStore.TryGetTransaction(cTx3.GetHash(), out var cTx3Same));
			Assert.Equal(uTx1, uTx1Same);
			Assert.Equal(uTx2, uTx2Same);
			Assert.Equal(uTx3, uTx3Same);
			Assert.Equal(cTx1, cTx1Same);
			Assert.Equal(cTx2, cTx2Same);
			Assert.Equal(cTx3, cTx3Same);
		}

		[Fact]
		public async Task CorrectsMempoolConfSeparateDupAsync()
		{
			PrepareTestEnv(out string dir, out Network network, out string mempoolFile, out string txFile, out SmartTransaction uTx1, out SmartTransaction uTx2, out SmartTransaction uTx3, out SmartTransaction cTx1, out SmartTransaction cTx2, out SmartTransaction cTx3);

			// Duplication in mempoool and confirmedtxs.
			var mempoolFileContent = new[]
			{
				uTx1.ToLine(),
				uTx2.ToLine(),
				uTx3.ToLine(),
				uTx2.ToLine()
			};
			var txFileContent = new[]
			{
				cTx1.ToLine(),
				cTx2.ToLine(),
				cTx3.ToLine(),
				cTx3.ToLine()
			};
			await File.WriteAllLinesAsync(mempoolFile, mempoolFileContent);
			await File.WriteAllLinesAsync(txFile, txFileContent);

			await using var txStore = new AllTransactionStore(dir, network);
			await txStore.InitializeAsync(ensureBackwardsCompatibility: false);

			Assert.Equal(6, txStore.GetTransactions().Count());
			Assert.Equal(6, txStore.GetTransactionHashes().Count());
			Assert.Equal(3, txStore.MempoolStore.GetTransactions().Count());
			Assert.Equal(3, txStore.MempoolStore.GetTransactionHashes().Count());
			Assert.Equal(3, txStore.ConfirmedStore.GetTransactions().Count());
			Assert.Equal(3, txStore.ConfirmedStore.GetTransactionHashes().Count());
		}

		[Fact]
		public async Task CorrectsLabelDupAsync()
		{
			PrepareTestEnv(out string dir, out Network network, out string mempoolFile, out string txFile, out SmartTransaction uTx1, out SmartTransaction uTx2, out SmartTransaction uTx3, out SmartTransaction cTx1, out SmartTransaction cTx2, out SmartTransaction cTx3);

			// Duplication is resolved with labels merged.
			var mempoolFileContent = new[]
			{
				new SmartTransaction(uTx1.Transaction, uTx1.Height, label: "buz, qux").ToLine(),
				new SmartTransaction(uTx2.Transaction, uTx2.Height, label: "buz, qux").ToLine(),
				new SmartTransaction(uTx2.Transaction, uTx2.Height, label: "foo, bar").ToLine(),
				uTx3.ToLine(),
				new SmartTransaction(cTx1.Transaction, Height.Mempool, label: new SmartLabel("buz", "qux")).ToLine()
			};
			var txFileContent = new[]
			{
				new SmartTransaction(cTx1.Transaction, cTx1.Height, label: new SmartLabel("foo", "bar")).ToLine(),
				cTx2.ToLine(),
				cTx3.ToLine(),
				new SmartTransaction(uTx1.Transaction, new Height(2), label: "foo, bar").ToLine()
			};
			await File.WriteAllLinesAsync(mempoolFile, mempoolFileContent);
			await File.WriteAllLinesAsync(txFile, txFileContent);

			await using var txStore = new AllTransactionStore(dir, network);
			await txStore.InitializeAsync(ensureBackwardsCompatibility: false);

			Assert.Equal(6, txStore.GetTransactions().Count());
			Assert.Equal(2, txStore.MempoolStore.GetTransactions().Count());
			Assert.Equal(4, txStore.ConfirmedStore.GetTransactions().Count());

			Assert.True(txStore.MempoolStore.Contains(uTx2.GetHash()));
			Assert.True(txStore.ConfirmedStore.Contains(uTx1.GetHash()));
			Assert.True(txStore.ConfirmedStore.Contains(cTx1.GetHash()));
			Assert.True(txStore.TryGetTransaction(uTx1.GetHash(), out _));
			Assert.True(txStore.TryGetTransaction(uTx2.GetHash(), out _));
			Assert.True(txStore.TryGetTransaction(cTx1.GetHash(), out _));
		}

		[Fact]
		public async Task CorrectsMempoolConfBetweenDupAsync()
		{
			PrepareTestEnv(out string dir, out Network network, out string mempoolFile, out string txFile, out SmartTransaction uTx1, out SmartTransaction uTx2, out SmartTransaction uTx3, out SmartTransaction cTx1, out SmartTransaction cTx2, out SmartTransaction cTx3);

			// Duplication between mempool and confirmed txs.
			var mempoolFileContent = new[]
			{
				uTx1.ToLine(),
				uTx2.ToLine(),
				uTx3.ToLine(),
				new SmartTransaction(cTx1.Transaction, Height.Mempool).ToLine()
			};
			var txFileContent = new[]
			{
				cTx1.ToLine(),
				cTx2.ToLine(),
				cTx3.ToLine(),
				new SmartTransaction(uTx1.Transaction, new Height(2)).ToLine()
			};
			await File.WriteAllLinesAsync(mempoolFile, mempoolFileContent);
			await File.WriteAllLinesAsync(txFile, txFileContent);

			await using var txStore = new AllTransactionStore(dir, network);
			await txStore.InitializeAsync(ensureBackwardsCompatibility: false);

			Assert.Equal(6, txStore.GetTransactions().Count());
			Assert.Equal(2, txStore.MempoolStore.GetTransactions().Count());
			Assert.Equal(4, txStore.ConfirmedStore.GetTransactions().Count());
		}

		[Fact]
		public async Task CorrectsOrderAsync()
		{
			string dir = PrepareWorkDir();
			var network = Network.TestNet;
			var mempoolFile = Path.Combine(dir, "Mempool", "Transactions.dat");
			var txFile = Path.Combine(dir, "ConfirmedTransactions", Constants.ConfirmedTransactionsVersion, "Transactions.dat");
			IoHelpers.EnsureContainingDirectoryExists(mempoolFile);
			IoHelpers.EnsureContainingDirectoryExists(txFile);

			var uTx1 = SmartTransaction.FromLine("34fc45781f2ac8e541b6045c2858c755dd2ab85e0ea7b5778b4d0cc191468571:01000000000102d5ae6e2612cdf8932d0e4f684d8ad9afdbca0afffba5c3dc0bf85f2b661bfb670000000000ffffffffbfd117908d5ba536624630519aaea7419605efa33bf1cb50c5ff7441f7b27a5b0100000000ffffffff01c6473d0000000000160014f9d25fe27812c3d35ad3819fcab8b95098effe15024730440220165730f8275434a5484b6aba3c71338a109b7cfd7380fdc18c6791a6afba9dee0220633b3b65313e57bdbd491d17232e6702912869d81461b4c939600d1cc99c06ec012102667c9fb272660cd6c06f853314b53c41da851f86024f9475ff15ea0636f564130247304402205e81562139231274cd7f705772c2810e34b7a291bd9882e6c567553babca9c7402206554ebd60d62a605a27bd4bf6482a533dd8dd35b12dc9d6abfa21738fdf7b57a012102b25dec5439a1aad8d901953a90d16252514a55f547fe2b85bc12e3f72cff1c4b00000000:Mempool::0::1570464578:False", network);
			var uTx2 = SmartTransaction.FromLine("b5cd5b4431891d913a6fbc0e946798b6f730c8b97f95a5c730c24189bed24ce7:01000000010113145dd6264da43406a0819b6e2d791ec9281322f33944ef034c3307f38c330000000000ffffffff0220a10700000000001600149af662cf9564700b93bd46fac8b51f64f2adad2343a5070000000000160014f1938902267eac1ae527128fe7773657d2b757b900000000:Mempool::0::1555590391:False", network);
			var uTx3 = SmartTransaction.FromLine("89e6788e63c5966fae0ccf6e85665ec62754f982b9593d7e3c4b78ac0e93208d:01000000000101f3e7c1bce1e0566800d8e6cae8f0d771a2ace8939cc6be7c8c21b05e590969530000000000ffffffff01cc2b0f0000000000160014e0ff6f42032bbfda63fabe0832b4ccb7be7350ae024730440220791e34413957c0f8348718d5d767f660657faf241801e74b5b81ac69e8912f60022041f3e9aeca137044565e1a81b6bcca74a88166436e5fa5f0e390448ac18fa5900121034dc07f3c26734591eb97f7e112888c3198d62bc3718106cba5a5688c62485b4500000000:Mempool::0::1555590448:False", network);
			var cTx1 = SmartTransaction.FromLine("95374c1037eb5268c8ae6681eb26756459d19754d41b660c251e6f62df586d29:0100000001357852bdf4e75a4ee2afe213463ff8afbed977ea5459a310777322504254ffdf0100000000ffffffff0240420f0000000000160014dc992214c430bf306fe446e9bac1dfc4ad4d3ee368cc100300000000160014005340d370675c47f7a04eb186200dd98c3d463c00000000:1580176:0000000034522ee38f074e1f4330b9c2f20c6a2b9a96de6f474a5f5f8fa76e2b:307::1569940579:False", network);
			var cTx2 = SmartTransaction.FromLine("af73b4c173da1bd24063e35a755babfa40728a282d6f56eeef4ce5a81ab26ee7:01000000013c81d2dcb25ad36781d1a6f9faa68f4a8b927f40e0b4e4b6184bb4761ebfc0dd0000000000ffffffff016f052e0000000000160014ae6e31ee9dae103f50979f761c2f9b44661da24f00000000:1580176:0000000034522ee38f074e1f4330b9c2f20c6a2b9a96de6f474a5f5f8fa76e2b:346::1569940633:False", network);
			var cTx3 = SmartTransaction.FromLine("ebcef423f6b03ef89dce076b53e624c966381f76e5d8b2b5034d3615ae950b2f:01000000000101296d58df626f1e250c661bd45497d159647526eb8166aec86852eb37104c37950100000000ffffffff01facb100300000000160014d5461e0e7077d62c4cf9c18a4e9ba10efd4930340247304402206d2c5b2b182474531ed07587e44ea22b136a37d5ddbd35aa2d984da7be5f7e5202202abd8435d9856e3d0892dbd54e9c05f2a20d9d5f333247314b925947a480a2eb01210321dd0574c773a35d4a7ebf17bf8f974b5665c0183598f1db53153e74c876768500000000:1580673:0000000017b09a77b815f3df513ff698d1f3b0e8c5e16ac0d6558e2d831f3bf9:130::1570462988:False", network);

			// Duplication in mempoool and confirmedtxs.
			var mempoolFileContent = new[]
			{
				uTx2.ToLine(),
				uTx1.ToLine(),
				uTx3.ToLine()
			};
			var txFileContent = new[]
			{
				cTx3.ToLine(),
				cTx1.ToLine(),
				cTx2.ToLine()
			};
			await File.WriteAllLinesAsync(mempoolFile, mempoolFileContent);
			await File.WriteAllLinesAsync(txFile, txFileContent);

			var expectedArray = new[]
			{
				uTx1,
				uTx2,
				uTx3,
				cTx1,
				cTx2,
				cTx3
			}.OrderByBlockchain().ToList();

			await using (var txStore = new AllTransactionStore(dir, network))
			{
				await txStore.InitializeAsync(ensureBackwardsCompatibility: false);

				var txs = txStore.GetTransactions();
				var txHashes = txStore.GetTransactionHashes();

				Assert.Equal(txHashes, txs.Select(x => x.GetHash()));
				Assert.Equal(expectedArray, txs);
			}

			await using (var txStore = new AllTransactionStore(PrepareWorkDir(), network))
			{
				await txStore.InitializeAsync(ensureBackwardsCompatibility: false);

				txStore.AddOrUpdate(uTx3);
				txStore.AddOrUpdate(uTx1);
				txStore.AddOrUpdate(uTx2);
				txStore.AddOrUpdate(cTx3);
				txStore.AddOrUpdate(cTx1);
				txStore.AddOrUpdate(cTx2);

				var txs = txStore.GetTransactions();
				var txHashes = txStore.GetTransactionHashes();

				Assert.Equal(txHashes, txs.Select(x => x.GetHash()));
				Assert.Equal(expectedArray, txs);
			}
		}

		[Theory]
		[MemberData(nameof(GetDifferentNetworkValues))]
		public async Task DoesntUpdateAsync(Network network)
		{
			await using var txStore = new AllTransactionStore(PrepareWorkDir(), network);
			await txStore.InitializeAsync(ensureBackwardsCompatibility: false);

			var tx = BitcoinFactory.CreateSmartTransaction();
			Assert.False(txStore.TryUpdate(tx));

			// Assert TryUpdate didn't modify anything.

			Assert.NotNull(txStore.ConfirmedStore);
			Assert.NotNull(txStore.MempoolStore);
			Assert.Empty(txStore.GetTransactions());
			Assert.Empty(txStore.GetTransactionHashes());
			Assert.Empty(txStore.MempoolStore.GetTransactions());
			Assert.Empty(txStore.MempoolStore.GetTransactionHashes());
			Assert.Empty(txStore.ConfirmedStore.GetTransactions());
			Assert.Empty(txStore.ConfirmedStore.GetTransactionHashes());

			uint256 txHash = BitcoinFactory.CreateSmartTransaction().GetHash();
			Assert.False(txStore.Contains(txHash));
			Assert.True(txStore.IsEmpty());
			Assert.False(txStore.TryGetTransaction(txHash, out _));
		}

		[Fact]
		public async Task ReorgAsync()
		{
			PrepareTestEnv(
				out string dir,
				out Network network,
				out string mempoolFile,
				out string txFile,
				out SmartTransaction uTx1,
				out SmartTransaction uTx2,
				out SmartTransaction uTx3,
				out SmartTransaction _,
				out SmartTransaction cTx2,
				out SmartTransaction cTx3);

			// Duplication is resolved with labels merged.
			var mempoolFileContent = new[]
			{
				uTx1.ToLine(),
				uTx2.ToLine(),
				uTx3.ToLine(),
			};
			var txFileContent = new[]
			{
				cTx2.ToLine(),
				cTx3.ToLine()
			};
			await File.WriteAllLinesAsync(mempoolFile, mempoolFileContent);
			await File.WriteAllLinesAsync(txFile, txFileContent);

			await using var txStore = new AllTransactionStore(dir, network);
			await txStore.InitializeAsync(ensureBackwardsCompatibility: false);

			// Two transactions are in the mempool store and unconfirmed.
			Assert.True(txStore.MempoolStore.TryGetTransaction(uTx1.GetHash(), out var myUnconfirmedTx1));
			Assert.False(myUnconfirmedTx1?.Confirmed);
			Assert.True(txStore.MempoolStore.TryGetTransaction(uTx2.GetHash(), out var myUnconfirmedTx2));
			Assert.False(myUnconfirmedTx2?.Confirmed);

			// Create the same transaction but now with a Height to make it confirmed.
			const int ReorgedBlockHeight = 34532;
			uint256 reorgedBlockHash = new(5);

			var tx1Confirmed = new SmartTransaction(uTx1.Transaction, new Height(ReorgedBlockHeight), blockHash: reorgedBlockHash, label: "buz, qux");
			var tx2Confirmed = new SmartTransaction(uTx2.Transaction, new Height(ReorgedBlockHeight), blockHash: reorgedBlockHash, label: "buz, qux");
			Assert.True(txStore.TryUpdate(tx1Confirmed));
			Assert.True(txStore.TryUpdate(tx2Confirmed));

			// Two transactions are in the ConfirmedStore store and confirmed.
			Assert.True(txStore.ConfirmedStore.TryGetTransaction(uTx1.GetHash(), out var mytx1));
			Assert.False(txStore.MempoolStore.TryGetTransaction(uTx1.GetHash(), out _));
			Assert.True(mytx1?.Confirmed);
			Assert.True(txStore.ConfirmedStore.TryGetTransaction(uTx2.GetHash(), out var mytx2));
			Assert.False(txStore.MempoolStore.TryGetTransaction(uTx2.GetHash(), out _));
			Assert.True(mytx2?.Confirmed);

			// Now reorg.
			txStore.ReleaseToMempoolFromBlock(reorgedBlockHash);

			// Two transactions are in the mempool store and unconfirmed.
			Assert.True(txStore.MempoolStore.TryGetTransaction(uTx1.GetHash(), out var myReorgedTx1));
			Assert.False(txStore.ConfirmedStore.TryGetTransaction(uTx1.GetHash(), out _));
			Assert.False(myReorgedTx1?.Confirmed);
			Assert.True(txStore.MempoolStore.TryGetTransaction(uTx2.GetHash(), out var myReorgedTx2));
			Assert.False(txStore.ConfirmedStore.TryGetTransaction(uTx2.GetHash(), out _));
			Assert.False(myReorgedTx2?.Confirmed);
		}

		[Fact]
		public async Task ReorgSameBlockAgainAsync()
		{
			int blocks = 300;
			int transactionsPerBlock = 3;
			string dir = PrepareWorkDir();

			var network = Network.Main;
			await using var txStore = new AllTransactionStore(dir, network);
			await txStore.InitializeAsync(ensureBackwardsCompatibility: false);

			foreach (var height in Enumerable.Range(1, blocks))
			{
				var blockHash = RandomUtils.GetUInt256();
				foreach (var n in Enumerable.Range(0, transactionsPerBlock))
				{
					txStore.AddOrUpdate(CreateTransaction(height, blockHash));
				}
			}
			var storedTxs = txStore.GetTransactions();
			Assert.Equal(blocks * transactionsPerBlock, storedTxs.Count());
			var newestConfirmedTx = storedTxs.Last();
			var tipHeight = blocks;
			var tipHash = newestConfirmedTx.BlockHash;
			Assert.Equal(tipHeight, newestConfirmedTx.Height.Value);

			// reorgs non-existing block
			var reorgedTxs = txStore.ReleaseToMempoolFromBlock(RandomUtils.GetUInt256());
			Assert.Empty(reorgedTxs);

			// reorgs most recent block
			reorgedTxs = txStore.ReleaseToMempoolFromBlock(newestConfirmedTx.BlockHash);
			Assert.Equal(3, reorgedTxs.Count());
			Assert.All(reorgedTxs, tx => Assert.False(tx?.Confirmed));
			Assert.All(reorgedTxs, tx => Assert.True(txStore.TryGetTransaction(tx.GetHash(), out _)));

			Assert.False(txStore.TryGetTransaction(tipHash, out _));
			Assert.DoesNotContain(tipHash, txStore.GetTransactionHashes());

			// reorgs the same block again
			reorgedTxs = txStore.ReleaseToMempoolFromBlock(newestConfirmedTx.BlockHash);
			Assert.Empty(reorgedTxs);
			Assert.False(txStore.TryGetTransaction(tipHash, out _));
			Assert.DoesNotContain(tipHash, txStore.GetTransactionHashes());

			// reorgs deep block
			var oldestConfirmedTx = storedTxs.First();
			var firstBlockHash = oldestConfirmedTx.BlockHash;

			// What to do here
			reorgedTxs = txStore.ReleaseToMempoolFromBlock(firstBlockHash);
			Assert.NotEmpty(reorgedTxs);
		}
	}
}
