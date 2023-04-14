using System.Collections.Generic;
using WalletWasabi.Blockchain.Transactions.Summary;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface ITransactionModel
{
	IEnumerable<Output> Outputs { get; set; }
	IEnumerable<Input> Inputs { get; set; }
}
