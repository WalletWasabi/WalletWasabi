using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels;

public abstract class TriggerCommandViewModel(UiContext uiContext) : RoutableViewModel(uiContext)
{
	public abstract ICommand TargetCommand { get; }
}
