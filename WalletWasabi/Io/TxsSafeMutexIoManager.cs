using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Io
{
	public class TxsSafeMutexIoManager : SafeMutexIoManager
	{
		private IEnumerable<SmartTransaction> Transactions { get; }

		public TxsSafeMutexIoManager(string filePath, IEnumerable<SmartTransaction> transactions) : base(filePath)
		{
			Transactions = Guard.NotNull(nameof(transactions), transactions);
		}
	}
}
