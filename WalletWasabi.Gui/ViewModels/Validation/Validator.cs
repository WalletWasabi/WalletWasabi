using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.ViewModels.Validation
{
	public static class Validator
	{
		public static IEnumerable<(string, MethodInfo)> PropertiesWithValidation(object instance)
		{
			foreach (PropertyInfo pInfo in ReflectionHelper.GetPropertyInfos(instance))
			{
				var vma = ReflectionHelper.GetAttribute<ValidateMethodAttribute>(pInfo);
				if (vma is null)
				{
					continue;
				}

				var mInfo = ReflectionHelper.GetMethodInfo<ErrorDescriptors>(instance, vma.MethodName);
				yield return (pInfo.Name, mInfo);
			}
		}

		public static ErrorDescriptors ValidateAllProperties(ViewModelBase viewModelBase, List<(string propertyName, MethodInfo mInfo)> validationMethodCache)
		{
			if (validationMethodCache is null)
			{
				return ErrorDescriptors.Empty;
			}

			ErrorDescriptors result = null;

			foreach (var validationCache in validationMethodCache)
			{
				var invokeRes = (ErrorDescriptors)validationCache.mInfo.Invoke(viewModelBase, null);

				if (result is null)
				{
					result = new ErrorDescriptors();
				}

				result.AddRange(invokeRes);
			}

			return result ?? ErrorDescriptors.Empty;
		}

		public static ErrorDescriptors ValidateProperty(ViewModelBase viewModelBase, string propertyName,
			List<(string propertyName, MethodInfo mInfo)> validationMethodCache)
		{
			if (validationMethodCache is null)
			{
				return ErrorDescriptors.Empty;
			}

			ErrorDescriptors result = null;

			foreach (var validationCache in validationMethodCache)
			{
				if (validationCache.propertyName != propertyName)
				{
					continue;
				}

				var invokeRes = (ErrorDescriptors)validationCache.mInfo.Invoke(viewModelBase, null);

				if (result is null)
				{
					result = new ErrorDescriptors();
				}

				result.AddRange(invokeRes);
			}

			return result ?? ErrorDescriptors.Empty;
		}
	}
}
