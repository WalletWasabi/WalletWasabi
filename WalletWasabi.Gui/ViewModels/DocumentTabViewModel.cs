using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using Dock.Model;

namespace WalletWasabi.Gui.ViewModels
{
	internal abstract class DocumentTabViewModel : ViewModelBase, IDocumentTabViewModel
	{
		public DocumentTabViewModel(string title)
		{
			Title = title;
		}

		public DocumentTabViewModel()
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
	}
}
