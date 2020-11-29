namespace WalletWasabi.Fluent.ViewModels.Navigation
{
	public enum NavigationMode
	{
		/// <summary>
		/// Navigates to another page with the current page saved on the stack.
		/// </summary>
		Normal,

		/// <summary>
		/// Navigates to another page without changing the stack.
		/// </summary>
		Skip,

		/// <summary>
		/// Navigates to another page replacing the current page on the stack.
		/// </summary>
		Swap,

		/// <summary>
		/// Navigates to another page and clears the stack. The back button will not be available after this.
		/// </summary>
		Clear
	}
}