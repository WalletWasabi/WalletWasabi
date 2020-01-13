using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Blockchain.Transactions.Operations;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions
{
	public class OperationMergerTests
	{
		[Fact]
		public void CanMergeSingleOperations()
		{
			var tx = Global.GenerateRandomSmartTransaction();

			var append = new Append(tx);
			var unmergedAppendOperations = new[] { append };
			var mergedOperations = OperationMerger.Merge(unmergedAppendOperations);
			var expectedOperation = Assert.Single(mergedOperations);
			var expectedAppendOperationInstance = Assert.IsType<Append>(expectedOperation);
			var expectedTx = Assert.Single(expectedAppendOperationInstance.Transactions);
			Assert.Equal(expectedTx, tx);

			var remove = new Remove(tx.GetHash());
			var unmergedRemoveOperations = new[] { remove };
			mergedOperations = OperationMerger.Merge(unmergedRemoveOperations);
			expectedOperation = Assert.Single(mergedOperations);
			var expectedRemoveOperationInstance = Assert.IsType<Remove>(expectedOperation);
			var expectedTxHash = Assert.Single(expectedRemoveOperationInstance.Transactions);
			Assert.Equal(expectedTxHash, tx.GetHash());

			var update = new Update(tx);
			var unmergedUpdateOperations = new[] { update };
			mergedOperations = OperationMerger.Merge(unmergedUpdateOperations);
			expectedOperation = Assert.Single(mergedOperations);
			var expectedUpdateOperationInstance = Assert.IsType<Update>(expectedOperation);
			expectedTx = Assert.Single(expectedUpdateOperationInstance.Transactions);
			Assert.Equal(expectedTx, tx);
		}

		[Fact]
		public void CanMergeComplexOperations()
		{
			var tx1 = Global.GenerateRandomSmartTransaction();
			var tx2 = Global.GenerateRandomSmartTransaction();
			var tx3 = Global.GenerateRandomSmartTransaction();
			var tx4 = Global.GenerateRandomSmartTransaction();
			var tx5 = Global.GenerateRandomSmartTransaction();
			var tx6 = Global.GenerateRandomSmartTransaction();
			var tx7 = Global.GenerateRandomSmartTransaction();
			var tx8 = Global.GenerateRandomSmartTransaction();

			var a1 = new Append(tx1, tx2);
			var a2 = new Append(tx3, tx4, tx5);
			var r1 = new Remove(tx6.GetHash());
			var a3 = new Append(tx6);
			var r2 = new Remove(tx1.GetHash(), tx2.GetHash());
			var r3 = new Remove(tx6.GetHash());
			var a4 = new Append(tx1);
			var u1 = new Update(tx7);
			var u2 = new Update(tx8);

			var unmergedOperations = new ITxStoreOperation[] { a1, a2, a3, a4, r1, r2, r3, u1, u2 };
			var mergedOperations = OperationMerger.Merge(unmergedOperations);
			Assert.Equal(3, mergedOperations.Count());

			unmergedOperations = new ITxStoreOperation[] { u1, u2, r1, r2, r3, a1, a2, a3, a4 };
			mergedOperations = OperationMerger.Merge(unmergedOperations);
			Assert.Equal(3, mergedOperations.Count());

			unmergedOperations = new ITxStoreOperation[] { a1, r1, u1, a2, r2, a3, r3, u2, a4 };
			mergedOperations = OperationMerger.Merge(unmergedOperations);
			Assert.Equal(9, mergedOperations.Count());

			unmergedOperations = new ITxStoreOperation[] { a1, a2, r1, a3, r2, r3, a4, u1, u2 };
			mergedOperations = OperationMerger.Merge(unmergedOperations);
			Assert.Equal(6, mergedOperations.Count());
			var mergedOperationsArray = mergedOperations.ToArray();
			Assert.Equal(5, Assert.IsType<Append>(mergedOperationsArray[0]).Transactions.Count());
			Assert.Single(Assert.IsType<Remove>(mergedOperationsArray[1]).Transactions);
			Assert.Single(Assert.IsType<Append>(mergedOperationsArray[2]).Transactions);
			Assert.Equal(3, Assert.IsType<Remove>(mergedOperationsArray[3]).Transactions.Count());
			Assert.Single(Assert.IsType<Append>(mergedOperationsArray[4]).Transactions);
			Assert.Equal(2, Assert.IsType<Update>(mergedOperationsArray[5]).Transactions.Count());
		}
	}
}
