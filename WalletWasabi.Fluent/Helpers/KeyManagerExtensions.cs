using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.Fluent.Helpers;

public static class KeyManagerExtensions
{
	public static (List<LabelsArray>, List<LabelsArray>) GetLabels(this KeyManager km)
	{
		var changeLabels = new List<LabelsArray>();
		var receiveLabels = new List<LabelsArray>();

		foreach (var key in km.GetKeys())
		{
			if (key.IsInternal)
			{
				changeLabels.Add(key.Labels);
			}
			else
			{
				receiveLabels.Add(key.Labels);
			}
		}

		return (changeLabels, receiveLabels);
	}
}
