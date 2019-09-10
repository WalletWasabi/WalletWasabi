using Avalonia;
using Avalonia.Data.Converters;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Converters
{
	public class ErrorDescriptorsJsonConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is null)
			{
				return ErrorDescriptors.Empty;
			}

			if (value is IEnumerable<Exception> exList)
			{
				return ExceptionListToErrorDescriptor(exList);
			}

			return ErrorDescriptors.Empty;
		}

		internal static ErrorDescriptors ExceptionListToErrorDescriptor(IEnumerable<Exception> exList)
		{
			var errors = new ErrorDescriptors();

			foreach (var exMsg in exList.Select(p => p.Message)
										.Where(p => p != null))
			{
				try
				{
					errors.AddRange(JsonConvert.DeserializeObject<ErrorDescriptors>(exMsg));
				}
				catch (Exception e)
				{
					errors.Add(new ErrorDescriptor(ErrorSeverity.Error, e.Message));
					errors.Add(new ErrorDescriptor(ErrorSeverity.Error, exMsg));
				}
			}

			return errors;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}
