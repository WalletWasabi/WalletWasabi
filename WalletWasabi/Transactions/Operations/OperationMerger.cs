using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WalletWasabi.Transactions.Operations
{
	public static class OperationMerger
	{
		/// <summary>
		/// Merges the operations in a way that it leaves the order, but two consecutive remove or append cannot follow each other.
		/// </summary>
		public static IEnumerable<ITxStoreOperation> Merge(IEnumerable<ITxStoreOperation> operations)
		{
			var tempToAppends = new List<SmartTransaction>();
			var tempToRemoves = new List<uint256>();
			var tempToUpdates = new List<SmartTransaction>();
			ITxStoreOperation prevOperation = operations.First();
			foreach (ITxStoreOperation op in operations)
			{
				if (prevOperation is Append)
				{
					if (op is Append opApp)
					{
						tempToAppends.AddRange(opApp.Transactions);
					}
					else
					{
						yield return new Append(tempToAppends);
						tempToAppends = new List<SmartTransaction>();

						if (op is Remove opRem)
						{
							tempToRemoves.AddRange(opRem.Transactions);
						}
						else if (op is Update opUpd)
						{
							tempToUpdates.AddRange(opUpd.Transactions);
						}
					}
				}
				else if (prevOperation is Remove)
				{
					if (op is Remove opRem)
					{
						tempToRemoves.AddRange(opRem.Transactions);
					}
					else
					{
						yield return new Remove(tempToRemoves);
						tempToRemoves = new List<uint256>();

						if (op is Append opApp)
						{
							tempToAppends.AddRange(opApp.Transactions);
						}
						else if (op is Update opUpd)
						{
							tempToUpdates.AddRange(opUpd.Transactions);
						}
					}
				}
				else if (prevOperation is Update)
				{
					if (op is Update opUpd)
					{
						tempToUpdates.AddRange(opUpd.Transactions);
					}
					else
					{
						yield return new Update(tempToUpdates);
						tempToUpdates = new List<SmartTransaction>();

						if (op is Append opApp)
						{
							tempToAppends.AddRange(opApp.Transactions);
						}
						else if (op is Remove opRem)
						{
							tempToRemoves.AddRange(opRem.Transactions);
						}
					}
				}

				prevOperation = op;
			}

			if (tempToAppends.Any())
			{
				yield return new Append(tempToAppends);
			}

			if (tempToRemoves.Any())
			{
				yield return new Remove(tempToRemoves);
			}

			if (tempToUpdates.Any())
			{
				yield return new Update(tempToUpdates);
			}
		}
	}
}
