namespace Gma.QrCodeNet.Encoding;

public struct VersionDetail
{
	internal VersionDetail(int version, int numTotalBytes, int numDataBytes, int numECBlocks)
		: this()
	{
		Version = version;
		NumTotalBytes = numTotalBytes;
		NumDataBytes = numDataBytes;
		NumECBlocks = numECBlocks;
	}

	internal int Version { get; private set; }
	internal int NumTotalBytes { get; private set; }
	internal int NumDataBytes { get; private set; }
	internal int NumECBlocks { get; private set; }

	internal int MatrixWidth => Width(Version);

	internal int ECBlockGroup1 => NumECBlocks - ECBlockGroup2;

	internal int ECBlockGroup2 => NumTotalBytes % NumECBlocks;

	internal int NumDataBytesGroup1 => NumDataBytes / NumECBlocks;

	internal int NumDataBytesGroup2 => NumDataBytesGroup1 + 1;

	internal int NumECBytesPerBlock => (NumTotalBytes - NumDataBytes) / NumECBlocks;

	internal static int Width(int version) => 17 + (4 * version);

	public override string ToString() => $"{Version};{NumTotalBytes};{NumDataBytes};{NumECBlocks}";
}
