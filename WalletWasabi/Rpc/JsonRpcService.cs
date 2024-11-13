using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace WalletWasabi.Rpc;

/// <summary>
/// Base class for service classes.
///
/// Provides two methods for responding to the clients with a **result** for valid
/// requests or with **error** in case the request is invalid or there is a problem.
///
/// Also, it loads and serves information about the service. It discovers
/// (using reflection) the methods that have to be invoked and the parameters it
/// receives.
/// </summary>
public class JsonRpcServiceMetadataProvider
{
	// Keeps the directory of procedures' metadata
	private Dictionary<string, JsonRpcMethodMetadata> _proceduresDirectory =
		new();

	private MethodInfo? _initializer = null;

	public JsonRpcServiceMetadataProvider(Type serviceType)
	{
		_serviceType = serviceType;
	}

	private readonly Type _serviceType;

	/// <summary>
	/// Tries to return the metadata for a given procedure name.
	/// Returns true if found otherwise returns false.
	/// </summary>
	public bool TryGetMetadata(string methodName, [NotNullWhen(true)] out JsonRpcMethodMetadata? metadata)
	{
		if (_proceduresDirectory.Count == 0)
		{
			LoadServiceMetadata();
		}

		if (!_proceduresDirectory.TryGetValue(methodName, out metadata))
		{
			metadata = null;
			return false;
		}

		return true;
	}

	public bool TryGetInitializer([NotNullWhen(true)] out MethodInfo? info)
	{
		info = _initializer;
		return info is not null;
	}

	private void LoadServiceMetadata()
	{
		_initializer = GetInitializationMethod();
		foreach (var info in EnumerateServiceInfo())
		{
			_proceduresDirectory.Add(info.Name, info);
		}
	}

	internal IEnumerable<JsonRpcMethodMetadata> EnumerateServiceInfo()
	{
		var publicMethods = _serviceType.GetMethods();
		foreach (var methodInfo in publicMethods)
		{
			var attrs = methodInfo.GetCustomAttributes();
			foreach (Attribute attr in attrs)
			{
				if (attr is JsonRpcMethodAttribute attribute)
				{
					var parameters = new List<(string name, Type type, bool isOptional, object defaultValue)>();
					foreach (var p in methodInfo.GetParameters())
					{
						parameters.Add((p.Name, p.ParameterType, p.IsOptional, p.DefaultValue));
					}

					var jsonRpcMethodAttr = attribute;
					yield return new JsonRpcMethodMetadata(
						jsonRpcMethodAttr.Name,
						methodInfo,
						jsonRpcMethodAttr.Initializable,
						parameters);
				}
			}
		}
	}

	internal MethodInfo? GetInitializationMethod()
	{
		var publicMethods = _serviceType.GetMethods();
		foreach (var methodInfo in publicMethods)
		{
			var attrs = methodInfo.GetCustomAttributes();
			foreach (Attribute attr in attrs)
			{
				if (attr is JsonRpcInitializationAttribute)
				{
					return methodInfo;
				}
			}
		}

		return null;
	}
}
