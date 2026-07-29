using System.Collections.Generic;
using Payjoin;

namespace WalletWasabi.Tests.UnitTests.Payjoin;

/// <summary>
/// Offline fixtures for driving the payjoin-ffi state machines in unit tests: a receiver
/// session created against a static OHTTP key config yields a real BIP 77 pj URI (fragment
/// params included) without any network I/O.
/// </summary>
internal static class PayjoinFfiTestHelpers
{
	/// <summary>OHTTP key config bytes (fixture from payjoin-ffi's own test suite).</summary>
	public static readonly byte[] OhttpKeysBytes =
	[
		0x01, 0x00, 0x16, 0x04, 0xba, 0x48, 0xc4, 0x9c, 0x3d, 0x4a,
		0x92, 0xa3, 0xad, 0x00, 0xec, 0xc6, 0x3a, 0x02, 0x4d, 0xa1,
		0x0c, 0xed, 0x02, 0x18, 0x0c, 0x73, 0xec, 0x12, 0xd8, 0xa7,
		0xad, 0x2c, 0xc9, 0x1b, 0xb4, 0x83, 0x82, 0x4f, 0xe2, 0xbe,
		0xe8, 0xd2, 0x8b, 0xfe, 0x2e, 0xb2, 0xfc, 0x64, 0x53, 0xbc,
		0x4d, 0x31, 0xcd, 0x85, 0x1e, 0x8a, 0x65, 0x40, 0xe8, 0x6c,
		0x53, 0x82, 0xaf, 0x58, 0x8d, 0x37, 0x09, 0x57, 0x00, 0x04,
		0x00, 0x01, 0x00, 0x03,
	];

	/// <summary>
	/// Canned Original PSBT paying <c>2MuyMrZHkbHbfjudmKUy45dU4P17pjG2szK</c>, copied verbatim
	/// from payjoin-test-utils' <c>ORIGINAL_PSBT</c> constant so sender tests keep their exact
	/// assertions without depending on the <c>_test-utils</c>-gated <c>PayjoinMethods.OriginalPsbt</c>
	/// helper (absent from the production package).
	/// </summary>
	public const string OriginalPsbt =
		"cHNidP8BAHMCAAAAAY8nutGgJdyYGXWiBEb45Hoe9lWGbkxh/6bNiOJdCDuDAAAAAAD+////AtyVuAUAAAAAF6kUHehJ8GnSdBUOOv6ujXLrWmsJRDCHgIQeAAAAAAAXqRR3QJbbz0hnQ8IvQ0fptGn+votneofTAAAAAAEBIKgb1wUAAAAAF6kU3k4ekGHKWRNbA1rV5tR5kEVDVNCHAQcXFgAUx4pFclNVgo1WWAdN1SYNX8tphTABCGsCRzBEAiB8Q+A6dep+Rz92vhy26lT0AjZn4PRLi8Bf9qoB/CMk0wIgP/Rj2PWZ3gEjUkTlhDRNAQ0gXwTO7t9n+V14pZ6oljUBIQMVmsAaoNWHVMS02LfTSe0e388LNitPa1UQZyOihY+FFgABABYAFEb2Giu6c4KO5YW0pfw3lGp9jMUUAAA=";

	/// <summary>
	/// The address must match what <see cref="OriginalPsbt"/> pays so the
	/// resulting URI can drive a <see cref="SenderBuilder"/> (payjoin-ffi suite fixture pair).
	/// </summary>
	public static PjUri CreatePjUri(string address = "2MuyMrZHkbHbfjudmKUy45dU4P17pjG2szK", string directory = "https://example.com")
	{
		using var ohttpKeys = OhttpKeys.Decode(OhttpKeysBytes);
		using var builder = new ReceiverBuilder(address, directory, ohttpKeys);
		using var transition = builder.Build();
		using var receiver = transition.Save(new InMemoryReceiverPersister());

		return receiver.PjUri();
	}
}

/// <summary>Throwaway receiver-side persister; sender tests only need the receiver for its URI.</summary>
internal class InMemoryReceiverPersister : JsonReceiverSessionPersister
{
	private readonly List<string> _events = new();

	public void Save(string @event) => _events.Add(@event);

	public string[] Load() => _events.ToArray();

	public void Close()
	{
	}
}
