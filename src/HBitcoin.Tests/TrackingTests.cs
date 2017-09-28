using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using HBitcoin.FullBlockSpv;
using HBitcoin.Models;
using NBitcoin;
using Xunit;

namespace HBitcoin.Tests
{
	public class TrackingTests
	{
		[Fact]
		public void TrackingBlockTest()
		{
            SmartMerkleBlock smartMerkleBlock = new SmartMerkleBlock(4, Network.Main.GetGenesis());
			var bytes = smartMerkleBlock.ToBytes();
			var same = SmartMerkleBlock.FromBytes(bytes);

			Assert.Equal(smartMerkleBlock.TransactionCount, same.TransactionCount);
			Assert.Equal(smartMerkleBlock.Height, same.Height);
			Assert.Equal(smartMerkleBlock.MerkleBlock.Header.GetHash(), same.MerkleBlock.Header.GetHash());

            var block = Network.Main.GetGenesis();
			var tx = new Transaction(
				"0100000001997ae2a654ddb2432ea2fece72bc71d3dbd371703a0479592efae21bf6b7d5100100000000ffffffff01e00f9700000000001976a9142a495afa8b8147ec2f01713b18693cb0a85743b288ac00000000");
			block.AddTransaction(tx);
			var tb2 = new SmartMerkleBlock(1, block, tx.GetHash());

			var bytes2 = tb2.ToBytes();
			var same2 = SmartMerkleBlock.FromBytes(bytes2);
			
			Assert.Equal(tb2.Height, same2.Height);
			Assert.Equal(tb2.MerkleBlock.Header.GetHash(), same2.MerkleBlock.Header.GetHash());
			var txid1 = tb2.GetMatchedTransactions().FirstOrDefault();
			var txid2 = same2.GetMatchedTransactions().FirstOrDefault();
			Assert.Equal(txid1, txid2);
		}
	}
}
