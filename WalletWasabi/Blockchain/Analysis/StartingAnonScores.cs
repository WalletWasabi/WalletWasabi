using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Blockchain.Analysis;

public class StartingAnonScores
{
	/// <summary>
	/// Anonymity score derived from all the inputs that goes as low as the lowest input's anonymity score.
	/// </summary>
	public (double standard, double sanctioned) Minimum { get; init; }

	/// <summary>
	/// Anonymity score derived from the largest inputs that goes as low as the lowest large input's anonymity score.
	/// </summary>
	public (double standard, double sanctioned) BigInputMinimum { get; init; }

	/// <summary>
	/// Anonymity score derived from the weighted average of all the inputs.
	/// </summary>
	public (double standard, double sanctioned) WeightedAverage { get; init; }
}
