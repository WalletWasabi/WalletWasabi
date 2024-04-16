using System.Collections.ObjectModel;

namespace WalletWasabi.Fluent.Controls;

public interface IUICommand
{
	public string Name { get; }
	public object Icon { get; }
	public IUICommand Command { get; }
}

public class UICommand : IUICommand
{
	public string Name { get; set; }
	public object Icon { get; set; }
	public IUICommand Command { get; }
}

public class UICommandCollection : Collection<IUICommand>
{
}
