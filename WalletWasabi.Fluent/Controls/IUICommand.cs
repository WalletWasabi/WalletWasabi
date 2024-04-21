using System.Windows.Input;

namespace WalletWasabi.Fluent.Controls;

public interface IUICommand
{
	public string Name { get; }
	public object Icon { get; }
	public ICommand Command { get; }
}
