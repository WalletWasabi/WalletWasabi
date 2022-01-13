using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace WalletWasabi.Fluent.Helpers;

public static class FileDialogHelper
{
	public static async Task<string?> ShowOpenFileDialogAsync(string title)
	{
		var ofd = CreateOpenFileDialog(title);
		return await GetDialogResultAsync(ofd);
	}

	public static async Task<string?> ShowOpenFileDialogAsync(string title, string[] filterExtTypes)
	{
		var ofd = CreateOpenFileDialog(title);
		ofd.Filters = GenerateFilters(filterExtTypes);
		return await GetDialogResultAsync(ofd);
	}

	public static async Task<string?> ShowSaveFileDialogAsync(string title, params string[] filterExtTypes)
	{
		var sfd = CreateSaveFileDialog(title, filterExtTypes);
		sfd.Filters = GenerateFilters(filterExtTypes);
		return await GetDialogResultAsync(sfd);
	}

	private static SaveFileDialog CreateSaveFileDialog(string title, IEnumerable<string> filterExtTypes)
	{
		var sfd = new SaveFileDialog
		{
			DefaultExtension = filterExtTypes.FirstOrDefault(),
			Title = title
		};

		return sfd;
	}

	private static async Task<string?> GetDialogResultAsync(OpenFileDialog ofd)
	{
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime &&
			lifetime.MainWindow is { })
		{
			var selected = await ofd.ShowAsync(lifetime.MainWindow);

			return selected?.FirstOrDefault();
		}

		return null;
	}

	private static async Task<string?> GetDialogResultAsync(SaveFileDialog sfd)
	{
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime &&
			lifetime.MainWindow is { })
		{
			return await sfd.ShowAsync(lifetime.MainWindow);
		}

		return null;
	}

	private static OpenFileDialog CreateOpenFileDialog(string title)
	{
		var ofd = new OpenFileDialog
		{
			AllowMultiple = false,
			Title = title,
		};

		SetDefaultDirectory(ofd);

		return ofd;
	}

	private static List<FileDialogFilter> GenerateFilters(string[] filterExtTypes)
	{
		var filters = new List<FileDialogFilter>();

		var generatedFilters =
			filterExtTypes
				.Where(x => x != "*")
				.Select(ext =>
					new FileDialogFilter
					{
						Name = $"{ext.ToUpper()} files",
						Extensions = new List<string> { ext }
					});

		filters.AddRange(generatedFilters);

		if (filterExtTypes.Contains("*"))
		{
			filters.Add(new FileDialogFilter()
			{
				Name = "All files",
				Extensions = new List<string> { "*" }
			});
		}

		return filters;
	}

	private static void SetDefaultDirectory(FileSystemDialog sfd)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			sfd.Directory = Path.Combine("/media", Environment.UserName);
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			sfd.Directory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		}
	}
}
