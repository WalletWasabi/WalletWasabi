using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace WalletWasabi.Gui.ViewModels.Validation
{
	public static class Validator
	{
		public static List<string> ValidateAllProperties(object instance)
		{
			var result = new List<string>();
			foreach (PropertyInfo property in ReflectionHelper.GetPropertyInfos(instance))
			{
				var errorString = ValidateMethod(instance, property);
				if (!string.IsNullOrEmpty(errorString))
				{
					result.Add(errorString);
				}
			}

			return result;
		}

		public static string ValidateProperty(object instance, string propertyName)
		{
			var property = ReflectionHelper.GetPropertyInfo(instance, propertyName);

			if (property != null)
			{
				return ValidateMethod(instance, property);
			}

			return string.Empty;
		}

		private static string ValidateMethod(object instance, PropertyInfo property)
		{
			var vma = ReflectionHelper.GetAttribute<ValidateMethodAttribute>(property);

			if (vma != null)
			{
				return ReflectionHelper.InvokeMethod<string>(instance, vma.MethodName);
			}
			else
			{
				return string.Empty;
			}
		}
	}
}
