using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.Clustering;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Labels;

public class RankInput
{
	public RankInput(IEnumerable<SmartLabel> receiveLabels, IEnumerable<SmartLabel> receiveAddressLabels, IEnumerable<SmartLabel> changeAddressLabels, IEnumerable<SmartLabel> transactionLabels)
	{
		ReceiveLabels = receiveLabels;
		ReceiveAddressLabels = receiveAddressLabels;
		ChangeAddressLabels = changeAddressLabels;
		TransactionLabels = transactionLabels;
	}

	public IEnumerable<SmartLabel> ReceiveLabels { get; }
	public IEnumerable<SmartLabel> ReceiveAddressLabels { get; }
	public IEnumerable<SmartLabel> ChangeAddressLabels { get; }
	public IEnumerable<SmartLabel> TransactionLabels { get; }
}
