using Avalonia.Markup.Xaml;
using System;
using System.Runtime.InteropServices;

namespace WalletWasabi.Fluent.Helpers
{
	public class PlatformExtension : MarkupExtension
	{
		public PlatformExtension()
		{
		}

		public PlatformExtension(object defaultValue)
		{
			Default = defaultValue;
		}

		public object Default { get; set; }

		public object Osx { get; set; }

		public object Linux { get; set; }

		public object Windows { get; set; }

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var result = Default;

			if (Osx != null && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				result = Osx;
			}
			else if (Linux != null && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				result = Linux;
			}
			else if (Windows != null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				result = Windows;
			}

			return result;
		}
	}
}
