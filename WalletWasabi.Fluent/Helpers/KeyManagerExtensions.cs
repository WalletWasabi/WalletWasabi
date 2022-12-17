using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;

namespace WalletWasabi.Fluent.Helpers;

public static class KeyManagerExtensions
{
	public static IEnumerable<SmartLabel> GetChangeLabels(this KeyManager km) =>
		km.GetKeys(isInternal: true).Select(x => x.Label);

	public static IEnumerable<SmartLabel> GetReceiveLabels(this KeyManager km) =>
		km.GetKeys(isInternal: false).Select(x => x.Label);

	public static void SetCoinjoinProfile(this KeyManager km, CoinJoinProfileViewModelBase profile)
	{
		km.RedCoinIsolation = profile.RedCoinIsolation;
		km.SetAnonScoreTarget(profile.AnonScoreTarget, toFile: false);
		km.SetFeeRateMedianTimeFrame(profile.FeeRateMedianTimeFrameHours, toFile: false);
		km.IsCoinjoinProfileSelected = true;

		km.ToFile();
	}
}
