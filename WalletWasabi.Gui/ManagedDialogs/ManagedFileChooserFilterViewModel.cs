using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.ManagedDialogs
{
	internal class ManagedFileChooserFilterViewModel : ViewModelBase
	{
		private readonly string[] Extensions;
		public string Name { get; }

		public ManagedFileChooserFilterViewModel(FileDialogFilter filter)
		{
			Name = filter.Name;

			if (filter.Extensions.Contains("*"))
			{
				return;
			}

			Extensions = filter.Extensions?.Select(e => "." + e.ToLowerInvariant()).ToArray();
		}

		public ManagedFileChooserFilterViewModel()
		{
			Name = "All files";
		}

		public bool Match(string filename)
		{
			if (Extensions == null)
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
