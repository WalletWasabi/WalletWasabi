using System.Collections.Generic;
using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

public interface ITransaction
{
	IObservable<int> Confirmations { get; }
	IEnumerable<InputViewModel> Inputs { get; }
	IEnumerable<OutputViewModel> Outputs { get; }
	DateTimeOffset Timestamp { get; }
	int IncludedInBlock { get; }
	Money OutputAmount => this.OutputAmount();
	Money? InputAmount => this.InputAmount();
	Money Amount { get; }
	string Id { get; }
	double Size { get; }
	int Version { get; }
	long BlockTime { get; }
	double Weight { get; }
	double VirtualSize { get; }
	IEnumerable<string> Labels { get; }
}
