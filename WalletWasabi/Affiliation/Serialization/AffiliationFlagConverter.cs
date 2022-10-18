using System.ComponentModel;
using System.Globalization;

namespace WalletWasabi.Affiliation.Serialization;

public class AffiliationFlagConverter : TypeConverter
{
	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		if (sourceType == typeof(string))
		{
			return true;
		}
		return base.CanConvertFrom(context, sourceType);
	}

	public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
	{
		if (value is string)
		{
			return new AffiliationFlag((string)value);
		}

		throw new NotSupportedException();
	}

	public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
	{
		if (destinationType == typeof(string))
		{
			if (value is AffiliationFlag)
			{
				return ((AffiliationFlag)value).Name;
			}
		}
		return base.ConvertTo(context, culture, value, destinationType);
	}
}
