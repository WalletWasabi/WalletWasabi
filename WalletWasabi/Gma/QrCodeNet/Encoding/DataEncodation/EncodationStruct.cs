using Gma.QrCodeNet.Encoding.Versions;

namespace Gma.QrCodeNet.Encoding.DataEncodation
{
	internal struct EncodationStruct
	{
		internal EncodationStruct(VersionControlStruct vcStruct) : this()
		{
			VersionDetail = vcStruct.VersionDetail;
		}

		internal VersionDetail VersionDetail { get; set; }
		internal BitList DataCodewords { get; set; }
	}
}
