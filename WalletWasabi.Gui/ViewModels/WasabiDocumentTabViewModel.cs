using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using Dock.Model;

namespace WalletWasabi.Gui.ViewModels
{
	public abstract class WasabiDocumentTabViewModel : ViewModelBase, IDocumentTabViewModel
	{
		public WasabiDocumentTabViewModel(string title)
		{
			Title = title;
		}

		public WasabiDocumentTabViewModel()
		{
		}

		public string Id { get; set; }
		public virtual string Title { get; set; }
		public object Context { get; set; }
		public double Width { get; set; }
		public double Height { get; set; }
		public IView Parent { get; set; }

		public virtual void Close()
		{
			IoC.Get<IShell>().RemoveDocument(this);
		}

		public virtual void OnSelected()
		{
		}

		public virtual void OnDeselected()
		{
		}
	}
}
