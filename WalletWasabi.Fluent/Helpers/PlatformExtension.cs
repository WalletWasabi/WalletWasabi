using System.ComponentModel;
using Avalonia.Markup.Xaml;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Data.Core;
using Avalonia.Utilities;
using System.Globalization;

namespace WalletWasabi.Fluent.Helpers;

public class PlatformExtension : MarkupExtension
{
	public PlatformExtension()
	{
	}

	public PlatformExtension(object defaultValue)
	{
		Default = defaultValue;
	}

	public object? Default { get; set; }

	public object? Osx { get; set; }

	public object? Linux { get; set; }

	public object? Windows { get; set; }

	public override object? ProvideValue(IServiceProvider serviceProvider)
	{
		var result = Default;

		if (Osx is not null && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			result = Osx;
		}
		else if (Linux is not null && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			result = Linux;
		}
		else if (Windows is not null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			result = Windows;
		}

		var provideValueTarget = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;

		if (provideValueTarget is { TargetProperty: IPropertyInfo propertyInfo})
		{
			if (TypeUtilities.TryConvert(
				    propertyInfo.PropertyType,
				    result,
				    CultureInfo.InvariantCulture,
				    out var converted))
			{
				return converted;
			}
		}

		return AvaloniaProperty.UnsetValue;
	}
}
