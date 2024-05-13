namespace WalletWasabi.Fluent.ViewModels.Navigation;

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
