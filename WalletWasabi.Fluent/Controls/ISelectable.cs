using System.ComponentModel;

namespace WalletWasabi.Fluent.Controls;

public interface ISelectable : INotifyPropertyChanged
{
    public bool IsSelected { get; set; }
}
