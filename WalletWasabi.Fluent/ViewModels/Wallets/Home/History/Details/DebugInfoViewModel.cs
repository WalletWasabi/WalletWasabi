using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Blockchain.Transactions.Summary;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.TreeDataGrid;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public class DebugInfoViewModel : ViewModelBase
{
	private readonly TransactionSummary _coinJoinTransaction;

	public DebugInfoViewModel(TransactionSummary coinJoinTransaction)
	{
		_coinJoinTransaction = coinJoinTransaction;

		OwnInputsSource = new FlatTreeDataGridSource<InputTransactionDebugInfo>(OwnInputs.OrderBy(x => x.Index))
		{
			Columns =
			{
				new PlainTextColumn<InputTransactionDebugInfo>("Amount", x => x.Amount.ToFormattedString(), GridLength.Auto, new ColumnOptions<InputTransactionDebugInfo>()),
				new PlainTextColumn<InputTransactionDebugInfo>("Index", x => x.Index.ToString(), GridLength.Auto, new ColumnOptions<InputTransactionDebugInfo>()),
				new PlainTextColumn<InputTransactionDebugInfo>("Address", x => x.Address.ToString(), GridLength.Auto, new ColumnOptions<InputTransactionDebugInfo>()),
				new PlainTextColumn<InputTransactionDebugInfo>("OutPoint", x => x.OutPoint.ToString(), GridLength.Auto, new ColumnOptions<InputTransactionDebugInfo>())
			}
		};
		OwnOutputsSource = new FlatTreeDataGridSource<OutputTransactionDebugInfo>(OwnOutputs.OrderBy(x => x.Index))
		{
			Columns =
			{
				new PlainTextColumn<OutputTransactionDebugInfo>("Amount", x => x.Amount.ToFormattedString(), GridLength.Auto, new ColumnOptions<OutputTransactionDebugInfo>()),
				new PlainTextColumn<OutputTransactionDebugInfo>("Index", x => x.Index.ToString(), GridLength.Auto, new ColumnOptions<OutputTransactionDebugInfo>()),
				new PlainTextColumn<OutputTransactionDebugInfo>("Anonscore", x => x.Anonscore.ToString(), GridLength.Auto, new ColumnOptions<OutputTransactionDebugInfo>()),
				new PlainTextColumn<OutputTransactionDebugInfo>("Equal outputs", x => x.EqualOutputs.ToString(), GridLength.Auto, new ColumnOptions<OutputTransactionDebugInfo>()),
				new PlainTextColumn<OutputTransactionDebugInfo>("Address", x => x.Address.ToString(), GridLength.Auto, new ColumnOptions<OutputTransactionDebugInfo>()),
			}
		};
	}

	public FlatTreeDataGridSource<InputTransactionDebugInfo> OwnInputsSource { get; set; }
	public FlatTreeDataGridSource<OutputTransactionDebugInfo> OwnOutputsSource { get; set; }

	public int InputCount => _coinJoinTransaction.Inputs.Count();
	public int OutputCount => _coinJoinTransaction.Outputs.Count();
	public Money SumOfOutputs => _coinJoinTransaction.Outputs.Sum(x => x.Amount);
	public Money? LargestOutputAmount => _coinJoinTransaction.Outputs.Max(x => x.Amount);
	public IEnumerable<InputTransactionDebugInfo> OwnInputs => _coinJoinTransaction.Inputs.OfType<KnownInput>().Select(x => new InputTransactionDebugInfo(x));
	public IEnumerable<OutputTransactionDebugInfo> OwnOutputs => _coinJoinTransaction.Outputs.OfType<KnownOutput>().Select(x => new OutputTransactionDebugInfo(x, _coinJoinTransaction.Outputs));
}
