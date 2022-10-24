using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Statistics;

[NavigationMetaData(Title = "Round State")]
public partial class RoundStateViewModel : RoutableViewModel
{
	[AutoNotify] private uint256 _id;
	[AutoNotify] private bool _isBlameRound;
	[AutoNotify] private int _inputCount;
	[AutoNotify] private decimal _maxSuggestedAmount;
	[AutoNotify] private TimeSpan _inputRegistrationRemaining;
	[AutoNotify] private Phase _phase;

	public RoundStateViewModel(RoundState roundState)
	{
		Id = roundState.Id;
		IsBlameRound = roundState.BlameOf != uint256.Zero;
		InputCount = roundState.CoinjoinState.Inputs.Count();
		MaxSuggestedAmount = roundState.CoinjoinState.Parameters.MaxSuggestedAmount.ToDecimal(MoneyUnit.BTC);
		InputRegistrationRemaining = roundState.InputRegistrationEnd - DateTimeOffset.UtcNow;
		Phase = roundState.Phase;
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
