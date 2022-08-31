namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

public static class ConvertSelection
{
	public static bool? From(TreeStateSelection state)
	{
		if (state == TreeStateSelection.False)
		{
			return false;
		}

		if (state == TreeStateSelection.True)
		{
			return true;
		}

		return null;
	}

	public static TreeStateSelection To(bool? state)
	{
		if (state == true)
		{
			return TreeStateSelection.True;
		}

		if (state == false)
		{
			return TreeStateSelection.False;
		}

		return TreeStateSelection.Partial;
	}
}
