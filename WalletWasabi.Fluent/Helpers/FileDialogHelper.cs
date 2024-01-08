using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Helpers;

public static class FileDialogHelper
{
	private static FilePickerFileType All { get; } = new("*")
    {
        Patterns = new[] { "*.*" },
        MimeTypes = new[] { "*/*" }
    };

    private static FilePickerFileType Json { get; } = new("json")
    {
        Patterns = new[] { "*.json" },
        AppleUniformTypeIdentifiers = new[] { "public.json" },
        MimeTypes = new[] { "application/json" }
    };

    private static FilePickerFileType Text { get; } = new("txt")
    {
        Patterns = new[] { "*.txt" },
        AppleUniformTypeIdentifiers = new[] { "public.text" },
        MimeTypes = new[] { "text/plain" }
    };

    private static FilePickerFileType Psbt { get; } = new("psbt")
    {
	    Patterns = new[] { "*.psbt" },
	    MimeTypes = new[] { "*/*" }
    };

    private static FilePickerFileType Txn { get; } = new("txn")
    {
	    Patterns = new[] { "*.txn" },
	    MimeTypes = new[] { "*/*" }
    };

    private static FilePickerFileType Png { get; } = new("png")
    {
	    Patterns = new[] { "*.png" },
	    AppleUniformTypeIdentifiers = new[] { "public.png" },
	    MimeTypes = new[] { "image/png" }
    };

    private static IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return window.StorageProvider;
        }

        if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: { } mainView })
        {
            var visualRoot = mainView.GetVisualRoot();
            if (visualRoot is TopLevel topLevel)
            {
                return topLevel.StorageProvider;
            }
        }

        return null;
    }

    private static List<FilePickerFileType> GetFilePickerFileTypes(string[] filterExtTypes)
    {
        var fileTypeFilters = new List<FilePickerFileType>();

        foreach (var fileType in filterExtTypes)
        {
            switch (fileType)
            {
                case "*":
                {
                    fileTypeFilters.Add(All);
                    break;
                }
                case "json":
                {
                    fileTypeFilters.Add(Json);
                    break;
                }
                case "txt":
                {
                    fileTypeFilters.Add(Text);
                    break;
                }
                case "psbt":
                {
	                fileTypeFilters.Add(Psbt);
	                break;
                }
                case "txn":
                {
	                fileTypeFilters.Add(Txn);
	                break;
                }
                case "png":
                {
	                fileTypeFilters.Add(Png);
	                break;
                }
            }
        }

        return fileTypeFilters;
    }

    public static async Task OpenFileAsync(Func<Stream, string, Uri, Task> callback, string[] filterExtTypes, string title, string? initialFileName = null, string? directory = null)
    {
	    // TODO: initialFileName is not supported, remove the parameter
        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var suggestedStartLocation = await GetSuggestedStartLocationAsync(directory, storageProvider);

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = suggestedStartLocation,
            FileTypeFilter = GetFilePickerFileTypes(filterExtTypes),
            AllowMultiple = false
        });

        var file = result.FirstOrDefault();
        if (file is not null)
        {
            await using var stream = await file.OpenReadAsync();
            await callback(stream, file.Name, file.Path);
        }
    }

    public static async Task SaveFileAsync(Func<Stream, string, Uri, Task> callback, string[] filterExtTypes, string title, string? initialFileName = null, string? directory = null)
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var suggestedStartLocation = await GetSuggestedStartLocationAsync(directory, storageProvider);

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            FileTypeChoices = GetFilePickerFileTypes(filterExtTypes),
            SuggestedFileName = initialFileName,
            DefaultExtension = filterExtTypes.FirstOrDefault(),
            SuggestedStartLocation = suggestedStartLocation,
            ShowOverwritePrompt = true
        });

        if (file is not null)
        {
            await using var stream = await file.OpenWriteAsync();
            await callback(stream, file.Name, file.Path);
        }
    }

	public static async Task<string?> ShowOpenFileDialogAsync(string title, string[] filterExtTypes, string? initialFileName = null, string? directory = null)
	{
		var ofd = CreateOpenFileDialog(title, directory);
		ofd.InitialFileName = initialFileName;
		ofd.Filters = GenerateFilters(filterExtTypes);
		return await GetDialogResultAsync(ofd);
	}

	public static async Task<string?> ShowSaveFileDialogAsync(string title, string[] filterExtTypes, string? initialFileName = null, string? directory = null)
	{
		var sfd = CreateSaveFileDialog(title, filterExtTypes, directory);
		sfd.InitialFileName = initialFileName;
		sfd.Filters = GenerateFilters(filterExtTypes);
		return await GetDialogResultAsync(sfd);
	}

	private static SaveFileDialog CreateSaveFileDialog(string title, IEnumerable<string> filterExtTypes, string? directory = null)
	{
		var sfd = new SaveFileDialog
		{
			DefaultExtension = filterExtTypes.FirstOrDefault(),
			Title = title,
			Directory = directory
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

	private static OpenFileDialog CreateOpenFileDialog(string title, string? directory = null)
	{
		var ofd = new OpenFileDialog
		{
			AllowMultiple = false,
			Title = title,
		};

		if (directory is null)
		{
			SetDefaultDirectory(ofd);
		}
		else
		{
			ofd.Directory = directory;
		}

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

	private static async Task<IStorageFolder?> GetSuggestedStartLocationAsync(string? directory, IStorageProvider storageProvider)
	{
		if (directory is not null)
		{
			return await storageProvider.TryGetFolderFromPathAsync(directory);
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return await storageProvider.TryGetFolderFromPathAsync(Path.Combine("/media", Environment.UserName));
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			return await storageProvider.TryGetFolderFromPathAsync(Environment.GetFolderPath(Environment.SpecialFolder.Personal));
		}

		return null;

	}
}
