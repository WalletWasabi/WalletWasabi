using System.Windows.Input;

namespace WalletWasabi.Fluent.Controls;

public interface ISortable
{
	public ICommand LabelDescending { get; }
	public ICommand LabelAscending { get; }
	public ICommand AmountDescending { get; }
	public ICommand AmountAscending { get; }
	public ICommand DateDescending { get; }
	public ICommand DateAscending { get; }
	public ICommand StatusDescending { get; }
	public ICommand StatusAscending { get; }
}
