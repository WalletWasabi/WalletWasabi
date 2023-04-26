using System.ComponentModel;

namespace WalletWasabi.Fluent;

public interface INavBarItem : INotifyPropertyChanged
{
	string Title { get; }
	string IconName { get; }
	string IconNameFocused { get; }
}
