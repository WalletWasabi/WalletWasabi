namespace WalletWasabi.Fluent.ViewModels.Navigation;

public enum NavigationMode
{
	/// <summary>
	/// Navigates to another page with the current page saved on the stack.
	/// </summary>
	Normal,

	/// <summary>
	/// Navigates to another page and clears the stack. The back button will not be available after this.
	/// </summary>
	Clear,

	/// <summary>
	/// Navigates to another page with the current page is not saved on the stack.
	/// </summary>
	Skip
}
