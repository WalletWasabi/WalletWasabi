using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.ManagedDialogs
{
	internal class ManagedFileChooserFilterViewModel : ViewModelBase
	{
		private IEnumerable<string> Extensions { get; }
		public string Name { get; }

		public ManagedFileChooserFilterViewModel(FileDialogFilter filter)
		{
			Name = filter.Name;

			if (filter.Extensions.Contains("*"))
			{
				return;
			}

			Extensions = filter.Extensions?.Select(e => "." + e.ToLowerInvariant());
		}

		public ManagedFileChooserFilterViewModel()
		{
			Name = "All files";
		}

		public bool Match(string filename)
		{
			if (Extensions is null)
			{
				return true;
			}

			foreach (var ext in Extensions)
			{
				if (filename.EndsWith(ext, StringComparison.InvariantCultureIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		public override string ToString() => Name;
	}
}
