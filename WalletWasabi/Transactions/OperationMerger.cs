using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Models;

namespace WalletWasabi.Transactions
{
	public static class OperationMerger
	{
		/// <summary>
		/// Merges the operations in a way that it leaves the order, but two consequtive remove or append cannot follow each other.
		/// </summary>
		public static IEnumerable<ITxStoreOperation> Merge(IEnumerable<ITxStoreOperation> operations)
		{
			var tempToAppends = new List<SmartTransaction>();
			var tempToRemoves = new List<uint256>();
			bool wasLastOpAppend = operations.First() is Append;
			foreach (ITxStoreOperation op in operations)
			{
				if (wasLastOpAppend)
				{
					if (op is Append opApp)
					{
						tempToAppends.AddRange(opApp.Transactions);
					}
					else if (op is Remove opRem)
					{
						yield return new Append(tempToAppends);
						tempToAppends = new List<SmartTransaction>();
						tempToRemoves.AddRange(opRem.Transactions);
					}
				}
				else
				{
					if (op is Remove opRem)
					{
						tempToRemoves.AddRange(opRem.Transactions);
					}
					else if (op is Append opApp)
					{
						yield return new Remove(tempToRemoves);
						tempToRemoves = new List<uint256>();
						tempToAppends.AddRange(opApp.Transactions);
					}
				}

				wasLastOpAppend = op is Append;
			}

			if (tempToAppends.Any())
			{
				yield return new Append(tempToAppends);
			}

			if (tempToRemoves.Any())
			{
				yield return new Remove(tempToRemoves);
			}
		}
	}
}
