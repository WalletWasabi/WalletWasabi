using System;
using System.Collections.Generic;
using System.Reflection;

namespace WalletWasabi.Gui.Rpc
{
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
				new Dictionary<string, JsonRpcMethodMetadata>();

		private Type ServiceType { get; }

		public JsonRpcServiceMetadataProvider(Type serviceType)
		{
			ServiceType = serviceType;
		}

		/// <summary>
		/// Tries to return the metadata for a given procedure name.
		/// Returns true if found otherwise returns false.
		/// </summary>
		public bool TryGetMetadata(string methodName, out JsonRpcMethodMetadata metadata)
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

		private void LoadServiceMetadata()
		{
			foreach (var info in EnumerateServiceInfo())
			{
				_proceduresDirectory.Add(info.Name, info);
			}
		}

		internal IEnumerable<JsonRpcMethodMetadata> EnumerateServiceInfo()
		{
			var publicMethods = ServiceType.GetMethods();
			foreach (var methodInfo in publicMethods)
			{
				var attrs = methodInfo.GetCustomAttributes();
				foreach (Attribute attr in attrs)
				{
					if (attr is JsonRpcMethodAttribute)
					{
						var parameters = new List<(string name, Type type)>();
						foreach (var p in methodInfo.GetParameters())
						{
							parameters.Add((p.Name, p.ParameterType));
						}
						var jsonRpcMethodAttr = (JsonRpcMethodAttribute)attr;
						yield return new JsonRpcMethodMetadata(jsonRpcMethodAttr.Name, methodInfo, parameters);
					}
				}
			}
		}
	}
}
