using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Serialization;

namespace WalletWasabi.Discoverability;

public class CoordinatorDirectory
{
	private readonly IReadOnlyList<KnownCoordinator> _all;

	public CoordinatorDirectory(IReadOnlyList<KnownCoordinator> all)
	{
		_all = all;
	}

	public IReadOnlyList<KnownCoordinator> For(Network network) =>
		_all.Where(c => c.Network == network).ToList();

	public static CoordinatorDirectory LoadBundled()
	{
		var path = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "Discoverability", "KnownCoordinators.json");
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("The bundled coordinator list was not found.", path);
		}

		var result = JsonDecoder.FromString(File.ReadAllText(path), Decode.Array(Decode.KnownCoordinator));
		if (result is null)
		{
			throw new InvalidDataException($"Could not parse coordinator list at '{path}'.");
		}
		return new CoordinatorDirectory(result);
	}
}
