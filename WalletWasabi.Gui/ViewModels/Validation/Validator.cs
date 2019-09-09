using System.Collections.Generic;
using System.Reflection;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.ViewModels.Validation
{
	public static class Validator
	{
		public static ErrorDescriptors ValidateAllProperties(object instance)
		{
			var result = new ErrorDescriptors();
			
			foreach (PropertyInfo property in ReflectionHelper.GetPropertyInfos(instance))
			{
				var error = ValidateMethod(instance, property);

				if (error.Equals(ErrorDescriptor.Default))
				{
					result.AddRange(error);
				}
			}

			return result;
		}

		public static ErrorDescriptors ValidateProperty(object instance, string propertyName)
		{
			var property = ReflectionHelper.GetPropertyInfo(instance, propertyName);

			if (property != null)
			{
				return ValidateMethod(instance, property);
			}

			return ErrorDescriptors.Empty;
		}

		private static ErrorDescriptors ValidateMethod(object instance, PropertyInfo property)
		{
			var vma = ReflectionHelper.GetAttribute<ValidateMethodAttribute>(property);

			if (vma != null)
			{
				return ReflectionHelper.InvokeMethod<ErrorDescriptors>(instance, vma.MethodName);
			}
			else
			{
				return ErrorDescriptors.Empty;
			}
		}
	}
}
