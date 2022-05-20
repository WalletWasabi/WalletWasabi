using Microsoft.CodeAnalysis;

namespace WalletWasabi.Fluent.Generators;

internal static class TypedStringHelpers
{
	public static string? ChoosePropertyName(string fieldName, TypedConstant overridenNameOpt)
	{
		if (!overridenNameOpt.IsNull)
		{
			return overridenNameOpt.Value?.ToString();
		}

		fieldName = fieldName.TrimStart('_');
		if (fieldName.Length == 0)
		{
			return string.Empty;
		}

		if (fieldName.Length == 1)
		{
			return fieldName.ToUpper();
		}

#pragma warning disable IDE0057 // Use range operator
		return fieldName.Substring(0, 1).ToUpper() + fieldName.Substring(1);
#pragma warning restore IDE0057 // Use range operator
	}
}