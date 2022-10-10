using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Statistics;

[NavigationMetaData(Title = "Round State")]
public partial class RoundStateViewModel : RoutableViewModel
{
	[AutoNotify] private uint256 _id;
	[AutoNotify] private uint256 _blameOf;
	[AutoNotify] private Phase _phase;
	[AutoNotify] private EndRoundState _endRoundState;
	[AutoNotify] private DateTimeOffset _inputRegistrationStart;
	[AutoNotify] private TimeSpan _inputRegistrationTimeout;

	public RoundStateViewModel(RoundState roundState)
	{
		Id = roundState.Id;
		BlameOf = roundState.BlameOf;
		// TODO: AmountCredentialIssuerParameters
		// TODO: VsizeCredentialIssuerParameters
		Phase = roundState.Phase;
		EndRoundState = roundState.EndRoundState;
		InputRegistrationStart = roundState.InputRegistrationStart;
		InputRegistrationTimeout = roundState.InputRegistrationTimeout;
		// TODO: CoinjoinState
	}

	public static Comparison<RoundStateViewModel?> SortAscending<T>(Func<RoundStateViewModel, T> selector)
	{
		return (x, y) =>
		{
			if (x is null && y is null)
			{
				return 0;
			}
			else if (x is null)
			{
				return -1;
			}
			else if (y is null)
			{
				return 1;
			}
			else
			{
				return Comparer<T>.Default.Compare(selector(x), selector(y));
			}
		};
	}

	public static Comparison<RoundStateViewModel?> SortDescending<T>(Func<RoundStateViewModel, T> selector)
	{
		return (x, y) =>
		{
			if (x is null && y is null)
			{
				return 0;
			}
			else if (x is null)
			{
				return 1;
			}
			else if (y is null)
			{
				return -1;
			}
			else
			{
				return Comparer<T>.Default.Compare(selector(y), selector(x));
			}
		};
	}
}
