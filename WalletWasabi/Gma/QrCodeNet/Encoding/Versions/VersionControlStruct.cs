namespace Gma.QrCodeNet.Encoding.Versions;

internal struct VersionControlStruct
{
	internal VersionDetail VersionDetail { get; set; }
	internal bool IsContainECI { get; set; }
	internal BitList ECIHeader { get; set; }
}
