using NBitcoin.Scripting;
using NBitcoin;

namespace WalletWasabi.Blockchain.Keys;

public class WpkhOutputDescriptorHelper
{
	/// <seealso href="https://github.com/bitcoin/bitcoin/blob/master/doc/descriptors.md#reference"/>
	public static WpkhDescriptors GetOutputDescriptors(Network network, HDFingerprint masterFingerprint, ExtKey notDerivedAccountKey, KeyPath accountKeyPath)
	{
		ExtKey accountKey = notDerivedAccountKey.Derive(accountKeyPath);

		// Optional part looks like this, for example: [2fc4a4f3/84'/0'/0']
		string optionalPart = $"[{masterFingerprint}/{accountKeyPath}]";

		// Optional part is followed by the actual private key: "tprv8ghYQhz7XQhoqDZG8SzbkqGCDTwAzyVVmUN3cUerPhUgK91Xvc4FaMJpYwrjuQ48WD7KdQ7Y6znKnaY9PXP8SiDLv1srjjs8NVYGuM7Hrrk"
		string privateDescriptor = $"{optionalPart}{accountKey.ToString(network)}";

		FlatSigningRepository internalRepo = new();
		FlatSigningRepository externalRepo = new();

		// Descriptor for all unhardened children (`/*`)
		OutputDescriptor internalPublicDescriptor = OutputDescriptor.Parse($"wpkh({privateDescriptor}/1/*)", network, requireCheckSum: false, internalRepo);
		OutputDescriptor externalPublicDescriptor = OutputDescriptor.Parse($"wpkh({privateDescriptor}/0/*)", network, requireCheckSum: false, externalRepo);

		externalPublicDescriptor.TryGetPrivateString(externalRepo, out string? privateExternalDescriptor);
		internalPublicDescriptor.TryGetPrivateString(internalRepo, out string? privateInternalDescriptor);

		return new(internalPublicDescriptor, externalPublicDescriptor, privateInternalDescriptor, privateExternalDescriptor);
	}

	/// <summary>
	/// Set of public and private + internal and external descriptors.
	/// </summary>
	/// <param name="PublicInternal">Descriptor describing something like:   <c>wpkh([2fc4a4f3/84'/0'/0']tpubDDPaZ82MfnPUigb426fCAEvJnVT7AJgQLmxptzh9oyH59dGJYzsqkqvgj6SyY9eBHhFmG286cfj66Dzv1kYAnC3o7LRxohvo7mwWPr26uje/1/*)#wjx95ths</c>.</param>
	/// <param name="PublicExternal">Descriptor describing something like:   <c>wpkh([2fc4a4f3/84'/0'/0']tpubDDPaZ82MfnPUigb426fCAEvJnVT7AJgQLmxptzh9oyH59dGJYzsqkqvgj6SyY9eBHhFmG286cfj66Dzv1kYAnC3o7LRxohvo7mwWPr26uje/0/*)#lxryf78g</c>.</param>
	/// <param name="PrivateInternal">A string similar to the following one: <c>wpkh([2fc4a4f3/84'/0'/0']tprv8ghYQhz7XQhoqDZG8SzbkqGCDTwAzyVVmUN3cUerPhUgK91Xvc4FaMJpYwrjuQ48WD7KdQ7Y6znKnaY9PXP8SiDLv1srjjs8NVYGuM7Hrrk/1/*)#6ny2206k</c>.</param>
	/// <param name="PrivateExternal">A string similar to the following one: <c>wpkh([2fc4a4f3/84'/0'/0']tprv8ghYQhz7XQhoqDZG8SzbkqGCDTwAzyVVmUN3cUerPhUgK91Xvc4FaMJpYwrjuQ48WD7KdQ7Y6znKnaY9PXP8SiDLv1srjjs8NVYGuM7Hrrk/0/*)#t8pth62w</c>.</param>
	public record WpkhDescriptors(OutputDescriptor PublicInternal, OutputDescriptor PublicExternal, string? PrivateInternal, string? PrivateExternal);
}
