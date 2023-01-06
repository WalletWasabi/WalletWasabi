using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Models.Serialization;

namespace WalletWasabi.Bases;

public abstract class ConfigBase : NotifyPropertyChangedBase, IConfig
{
	protected ConfigBase()
	{
	}

	protected ConfigBase(string filePath)
	{
		SetFilePath(filePath);
	}

	/// <inheritdoc />
	public string FilePath { get; private set; } = "";

	private object FileLocker { get; } = new();

	/// <inheritdoc />
	public void AssertFilePathSet()
	{
		if (string.IsNullOrWhiteSpace(FilePath))
		{
			throw new NotSupportedException($"{nameof(FilePath)} is not set. Use {nameof(SetFilePath)} to set it.");
		}
	}

	/// <inheritdoc />
	public bool CheckFileChange()
	{
		AssertFilePathSet();

		if (!File.Exists(FilePath))
		{
			throw new FileNotFoundException($"{GetType().Name} file did not exist at path: `{FilePath}`.");
		}

		string jsonString;
		lock (FileLocker)
		{
			jsonString = File.ReadAllText(FilePath, Encoding.UTF8);
		}

		var newConfigObject = Activator.CreateInstance(GetType())!;
		JsonConvert.PopulateObject(jsonString, newConfigObject, new JsonSerializerSettings()
		{
			Converters = JsonSerializationOptions.Default.Settings.Converters, ObjectCreationHandling = ObjectCreationHandling.Replace
		});

		return !AreDeepEqual(newConfigObject);
	}

	/// <inheritdoc />
	public virtual void LoadOrCreateDefaultFile()
	{
		AssertFilePathSet();
		var create = false;
		if (!File.Exists(FilePath))
		{
			Logger.LogInfo($"{GetType().Name} file did not exist. Created at path: `{FilePath}`.");
			create = true;
		}
		else
		{
			try
			{
				LoadFile();
			}
			catch (Exception ex)
			{
				Logger.LogInfo($"{GetType().Name} file has been deleted because it was corrupted. Recreated default version at path: `{FilePath}`.");
				Logger.LogWarning(ex);
				create = true;
			}
		}

		if (create)
		{
			
			JsonConvert.PopulateObject("{}", this);
			ToFile();
		}
	}

	/// <inheritdoc />
	public virtual void LoadFile()
	{
		string jsonString;
		lock (FileLocker)
		{
			jsonString = File.ReadAllText(FilePath, Encoding.UTF8);
		}
Update(jsonString,TryEnsureBackwardsCompatibility(jsonString));

	}

	/// <inheritdoc />
	public void SetFilePath(string path)
	{
		FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(path), path, trim: true);
	}

	/// <inheritdoc />
	public bool AreDeepEqual(object otherConfig)
	{
		var serializer = JsonSerializer.Create(JsonSerializationOptions.Default.Settings);
		serializer.ObjectCreationHandling = ObjectCreationHandling.Replace;
		var currentConfig = JObject.FromObject(this, serializer);
		var otherConfigJson = JObject.FromObject(otherConfig, serializer);
		return JToken.DeepEquals(otherConfigJson, currentConfig);
	}

	/// <inheritdoc />
	public void ToFile()
	{
		AssertFilePathSet();
		string jsonString = ToString();
		lock (FileLocker)
		{
			File.WriteAllText(FilePath, jsonString, Encoding.UTF8);
		}
	}

	public void Update(string json, bool persist)
	{
		
		var serializer = JsonSerializer.Create(JsonSerializationOptions.Default.Settings);
		serializer.ObjectCreationHandling = ObjectCreationHandling.Replace;
		
		JsonConvert.PopulateObject(json, this, new JsonSerializerSettings()
		{
			Converters = JsonSerializationOptions.Default.Settings.Converters, ObjectCreationHandling = ObjectCreationHandling.Replace
		});
		if (persist)
		{
			ToFile();
		}
	}
	public override string ToString()
	{
		return JsonConvert.SerializeObject(this, Formatting.Indented, JsonSerializationOptions.Default.Settings);
	}

	protected virtual bool TryEnsureBackwardsCompatibility(string jsonString) => true;
}
