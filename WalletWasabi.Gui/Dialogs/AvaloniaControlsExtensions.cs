using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace Avalonia.Controls
{
	public static class AvaloniaControlsExtensions
	{
		public static async Task<string> ShowAsync(this SaveFileDialog me, Window parent, bool fallBack)
		{
			var ret = await ShowAsync(me as FileDialog, parent, fallBack);
			return Guard.Correct(ret?.FirstOrDefault());
		}

		public static async Task<string[]> ShowAsync(this OpenFileDialog me, Window parent, bool fallBack)
		{
			return await ShowAsync(me as FileDialog, parent, fallBack);
		}

		private static async Task<string[]> ShowAsync(this FileDialog me, Window parent, bool fallBack)
		{
			if (fallBack)
			{
				try
				{
					return await ShowOpenSaveFileDialogAsync(me, parent);
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex);

					string title = !string.IsNullOrWhiteSpace(me.Title)
						? me.Title
						: me is OpenFileDialog
							? "Open File"
							: me is SaveFileDialog
								? "Save File"
								: throw new NotImplementedException();

					string instructions = me is OpenFileDialog
						? $"Failed to use your operating system's {nameof(OpenFileDialog)}. Please provide the path of the file you want to open manually:"
						: me is SaveFileDialog
							? $"Failed to use your operating system's {nameof(SaveFileDialog)}. Please provide the path where you want your file to be saved to:"
							: throw new NotImplementedException();

					string exampleFilePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
						? @"C:\path\to\the\file.ext"
						: @"/path/to/the/file";

					string defaultTextInput = me is OpenFileDialog
						? Path.Combine(me.Directory ?? "", me.InitialFileName ?? "")
						: me is SaveFileDialog sfd
							? Path.ChangeExtension(
								Path.Combine(
									string.IsNullOrEmpty(me.Directory) ? Path.GetDirectoryName(exampleFilePath) : me.Directory,
									string.IsNullOrEmpty(me.InitialFileName) ? Path.GetFileName(exampleFilePath) : me.InitialFileName),
								string.IsNullOrEmpty(sfd.DefaultExtension) ? "ext" : sfd.DefaultExtension)
							: throw new NotImplementedException();

					var dialog = new TextInputDialogViewModel(title, instructions, exampleFilePath, defaultTextInput);
					var success = await MainWindowViewModel.Instance.ShowDialogAsync(dialog);

					if (success)
					{
						var path = dialog.TextInput.Trim();
						return new string[] { path };
					}
					else
					{
						return Array.Empty<string>();
					}
				}
			}
			else
			{
				return await ShowOpenSaveFileDialogAsync(me, parent);
			}
		}

		private static async Task<string[]> ShowOpenSaveFileDialogAsync(FileDialog me, Window parent)
		{
			if (me is OpenFileDialog ofd)
			{
				return await ofd.ShowAsync(parent);
			}
			else if (me is SaveFileDialog sfd)
			{
				return new string[] { await sfd.ShowAsync(parent) };
			}
			else
			{
				throw new NotImplementedException();
			}
		}
	}
}
