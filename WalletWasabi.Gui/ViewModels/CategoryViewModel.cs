namespace WalletWasabi.Gui.ViewModels
{
	internal class CategoryViewModel : ViewModelBase
	{
		public CategoryViewModel(string title)
		{
			Title = title;
		}

		public string Title { get; }

		public virtual void OnCategorySelected()
		{
		}
	}
}
