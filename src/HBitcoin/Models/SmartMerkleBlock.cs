using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace HBitcoin.Models
{
	public class SmartMerkleBlock : IEquatable<SmartMerkleBlock>, IComparable<SmartMerkleBlock>
	{
		#region Members
		
		public Height Height { get; }
		public MerkleBlock MerkleBlock { get; }

		public IEnumerable<uint256> GetMatchedTransactions() => MerkleBlock.PartialMerkleTree.GetMatchedTransactions();
		public uint TransactionCount => MerkleBlock.PartialMerkleTree.TransactionCount;

		#endregion

		#region Constructors

		public SmartMerkleBlock()
		{

		}

		public SmartMerkleBlock(Height height, Block block, params uint256[] interestedTransactionIds)
		{
			Height = height;
			MerkleBlock = interestedTransactionIds == null || interestedTransactionIds.Length == 0 ? block.Filter() : block.Filter(interestedTransactionIds);
		}
		public SmartMerkleBlock(int height, Block block, params uint256[] interestedTransactionIds)
		{
			Height = new Height(height);
			MerkleBlock = interestedTransactionIds == null || interestedTransactionIds.Length == 0 ? block.Filter() : block.Filter(interestedTransactionIds);
		}

		public SmartMerkleBlock(Height height, MerkleBlock merkleBlock)
		{
			Height = height;
			MerkleBlock = merkleBlock;
		}

		#endregion

		#region Formatting

		public static byte[] ToBytes(SmartMerkleBlock smartMerkleBlock) => 
			BitConverter.GetBytes(smartMerkleBlock.Height.Value) // 4bytes
			.Concat(smartMerkleBlock.MerkleBlock.ToBytes())
			.ToArray();

		public byte[] ToBytes() => ToBytes(this);

		public static SmartMerkleBlock FromBytes(byte[] bytes)
		{
			var heightBytes = bytes.Take(4).ToArray();
			var merkleBlockBytes = bytes.Skip(4).ToArray();

			var height = new Height(BitConverter.ToInt32(heightBytes, startIndex: 0));

            var merkleBlock = new MerkleBlock();
            merkleBlock.FromBytes(merkleBlockBytes);

            return new SmartMerkleBlock(height, merkleBlock);
		}

		#endregion

		#region EqualityAndComparison

		public override bool Equals(object obj) => obj is SmartMerkleBlock && this == (SmartMerkleBlock)obj;
		public bool Equals(SmartMerkleBlock other) => this == other;
		public override int GetHashCode()
		{
			var hash = Height.GetHashCode();
			hash = hash ^ MerkleBlock.Header.GetHash().GetHashCode();
			hash = hash ^ MerkleBlock.Header.HashPrevBlock.GetHashCode();
			hash = hash ^ MerkleBlock.Header.HashMerkleRoot.GetHashCode();
			foreach(uint256 txhash in GetMatchedTransactions())
				hash = hash ^ txhash.GetHashCode();

			return hash;
		}

		public static bool operator ==(SmartMerkleBlock x, SmartMerkleBlock y)
		{
			if (x.Height != y.Height)
				return false;

			if(x.MerkleBlock.Header.GetHash() != y.MerkleBlock.Header.GetHash())
				return false;
			if (x.MerkleBlock.Header.HashPrevBlock != y.MerkleBlock.Header.HashPrevBlock)
				return false;

			if (x.MerkleBlock.Header.HashMerkleRoot != y.MerkleBlock.Header.HashMerkleRoot)
				return false;
			if (x.TransactionCount != y.TransactionCount)
				return false;
			if(x.TransactionCount == 0) return true;

			if (!x.GetMatchedTransactions().SequenceEqual(y.GetMatchedTransactions()))
				return false;

			return true;
		}

		public static bool operator !=(SmartMerkleBlock x, SmartMerkleBlock y) => !(x == y);

		public int CompareTo(SmartMerkleBlock other) => Height.CompareTo(other.Height);

		public static bool operator >(SmartMerkleBlock x, SmartMerkleBlock y) => x.Height > y.Height;
		public static bool operator <(SmartMerkleBlock x, SmartMerkleBlock y) => x.Height < y.Height;
		public static bool operator >=(SmartMerkleBlock x, SmartMerkleBlock y) => x.Height >= y.Height;
		public static bool operator <=(SmartMerkleBlock x, SmartMerkleBlock y) => x.Height <= y.Height;

		#endregion
	}
}
