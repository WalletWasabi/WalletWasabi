using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Navigation;

public static class NavigationExtensions
{
	public static async Task<DialogResult<T>> NavigateDialogAsync<T>(
		this TargettedNavigationStack stack,
		DialogViewModelBase<T> dialog)
	{
		stack.To(dialog);

		var result = await dialog.GetDialogResultAsync();

		stack.Back();

		return result;
	}
}

public class TargettedNavigationStack : NavigationStack<RoutableViewModel>
{
	private readonly NavigationTarget _target;

	public TargettedNavigationStack(NavigationTarget target)
	{
		_target = target;
	}

	public override void Clear()
	{
		if (_target == NavigationTarget.HomeScreen)
		{
			base.Clear(true);
		}
		else
		{
			base.Clear();
		}
	}

	protected override void OnPopped(RoutableViewModel page)
	{
		base.OnPopped(page);

		page.CurrentTarget = NavigationTarget.Default;
	}

	protected override void OnNavigated(
		RoutableViewModel? oldPage,
		bool oldInStack,
		RoutableViewModel? newPage,
		bool newInStack)
	{
		base.OnNavigated(oldPage, oldInStack, newPage, newInStack);

		if (oldPage is { } && oldPage != newPage)
		{
			oldPage.IsActive = false;
		}

		if (newPage is { })
		{
			newPage.CurrentTarget = _target;
		}
	}
}
