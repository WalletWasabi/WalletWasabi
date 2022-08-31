using WalletWasabi.Fluent.ViewModels.CoinSelection.Model;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Columns;

public static class ConvertSelection
{
	public static bool? From(SelectionState state)
	{
		if (state == SelectionState.False)
		{
			return false;
		}

		if (state == SelectionState.True)
		{
			return true;
		}

		return null;
	}

	public static SelectionState To(bool? state)
	{
		if (state == true)
		{
			return SelectionState.True;
		}

		if (state == false)
		{
			return SelectionState.False;
		}

		return SelectionState.Partial;
	}
}