using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.ViewModels.Validation
{
	public delegate void AddErrorDelegate(ErrorSeverity severity, string error);

	public delegate void ValidateMethod(AddErrorDelegate addError);

	public static class Validator
	{
		//public static IEnumerable<(string propertyName, ErrorDescriptors errors)> ValidateAllProperties(Dictionary<string, ValidateMethod> validationMethodCache)
		//{
		//	if(validationMethodCache is null || validationMethodCache.Count == 0)
		//	{
		//		throw new Exception("Cant call ValidateAllProperties on ViewModels with no ValidateAttributes");
		//	}

		//	var result = new List<(string propertyName, ErrorDescriptors errors)>();

		//	foreach (var propertyName in validationMethodCache.Keys)
		//	{
			
		//		var invokeRes = (ErrorDescriptors)validationMethodCache[propertyName].Invoke();

		//		result.Add((propertyName, invokeRes));
		//	}

		//	return result;
		//}

		//public static ErrorDescriptors ValidateProperty(ViewModelBase viewModelBase, string propertyName,
		//	Dictionary<string, MethodInfo> validationMethodCache)
		//{
		//	if (validationMethodCache is null)
		//	{
		//		return ErrorDescriptors.Empty;
		//	}

		//	ErrorDescriptors result = null;

		//	if(validationMethodCache.ContainsKey(propertyName))
		//	{
		//		var invokeRes = (ErrorDescriptors)validationMethodCache[propertyName].Invoke(viewModelBase, null);

		//		if (result is null)
		//		{
		//			result = new ErrorDescriptors();
		//		}

		//		result.AddRange(invokeRes);
		//	}

		//	return result ?? ErrorDescriptors.Empty;
		//}
	}
}
