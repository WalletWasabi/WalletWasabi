namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public interface ISelectable
{
	public bool IsSelected { get; set; }
}

public interface IThreeState
{
	public SelectionState SelectionState { get; set; }
}

public enum SelectionState
{
	False,
	True,
	Partial,
}
