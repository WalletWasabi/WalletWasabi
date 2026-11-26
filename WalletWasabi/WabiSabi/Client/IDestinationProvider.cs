namespace WalletWasabi.WabiSabi.Client;

public interface IDestinationProvider
{
	ImmutableHashSet<ScriptType> SupportedScriptTypes { get; }

	/// <summary>
	/// Gets destinations for <paramref name="holder"/>. Nobody else gets them until the holder releases them.
	/// </summary>
	IEnumerable<IDestination> GetNextDestinations(int count, bool preferTaproot, object holder);

	/// <summary>
	/// Makes the destinations that <paramref name="holder"/> got and that were not used available again.
	/// </summary>
	void ReleaseDestinations(object holder);

	public void TrySetScriptStates(KeyState state, IEnumerable<Script> scripts);
}
