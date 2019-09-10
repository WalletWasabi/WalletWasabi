using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WalletWasabi.Gui.ViewModels.Validation
{
	public static class ReflectionHelper
	{
		public static PropertyInfo GetPropertyInfo(object instance, string propertyName)
		{
			return instance.GetType().GetRuntimeProperty(propertyName);
		}

		public static PropertyInfo GetPropertyInfo(Type type, string propertyName)
		{
			return type.GetRuntimeProperty(propertyName);
		}

		public static T GetAttribute<T>(PropertyInfo property) where T : Attribute
		{
			return GetAttributes<T>(property).FirstOrDefault();
		}

		public static T GetAttribute<T>(object instance, string propertyPath) where T : Attribute
		{
			var property = GetPropertyInfo(instance, propertyPath);

			return GetAttribute<T>(property);
		}

		public static IEnumerable<T> GetAttributes<T>(PropertyInfo property) where T : Attribute
		{
			return property.GetCustomAttributes<T>();
		}

		public static T InvokeMethod<T>(object instance, string methodName)
		{
			MethodInfo info = instance.GetType().GetRuntimeMethod(methodName, new Type[0]);

			if (info != null &&
				info.ReturnType == typeof(T) &&
				info.GetParameters().Length == 0)
			{
				return (T)info.Invoke(instance, null);
			}
			else
			{
				throw new ArgumentException("Method was not found on class");
			}
		}

		public static MethodInfo GetMethodInfo<T>(object instance, string methodName)
		{
			MethodInfo info = instance.GetType().GetRuntimeMethod(methodName, new Type[0]);

			if (info != null &&
				info.ReturnType == typeof(T) &&
				info.GetParameters().Length == 0)
			{
				return info;
			}
			else
			{
				throw new ArgumentException("Method was not found on class");
			}
		}

		public static IEnumerable<PropertyInfo> GetPropertyInfos(object instance)
		{
			return instance.GetType().GetRuntimeProperties();
		}
	}
}
