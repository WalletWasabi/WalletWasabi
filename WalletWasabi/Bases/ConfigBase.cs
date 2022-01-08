using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;

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
	public string FilePath { get; private set; } = null;

	/// <inheritdoc />
	public void AssertFilePathSet()
	{
		if (FilePath is null)
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

		string jsonString = File.ReadAllText(FilePath, Encoding.UTF8);

		var newConfigObject = Activator.CreateInstance(GetType());
		JsonConvert.PopulateObject(jsonString, newConfigObject);

		return !AreDeepEqual(newConfigObject);
	}

	/// <inheritdoc />
	public virtual void LoadOrCreateDefaultFile()
	{
		AssertFilePathSet();
		JsonConvert.PopulateObject("{}", this);

		if (!File.Exists(FilePath))
		{
			Logger.LogInfo($"{GetType().Name} file did not exist. Created at path: `{FilePath}`.");
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
			}
		}

		ToFile();
	}

	/// <inheritdoc />
	public virtual void LoadFile()
	{
		var jsonString = File.ReadAllText(FilePath, Encoding.UTF8);

		JsonConvert.PopulateObject(jsonString, this);

		if (TryEnsureBackwardsCompatibility(jsonString))
		{
			ToFile();
		}
	}

	/// <inheritdoc />
	public void SetFilePath(string path)
	{
		FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(path), path, trim: true);
	}

	/// <inheritdoc />
	public bool AreDeepEqual(object otherConfig)
	{
		var currentConfig = JObject.FromObject(this);
		var otherConfigJson = JObject.FromObject(otherConfig);
		return JToken.DeepEquals(otherConfigJson, currentConfig);
	}

	/// <inheritdoc />
	public void ToFile()
	{
		AssertFilePathSet();

		string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
		File.WriteAllText(FilePath, jsonString, Encoding.UTF8);
	}

	protected virtual bool TryEnsureBackwardsCompatibility(string jsonString) => true;
}
