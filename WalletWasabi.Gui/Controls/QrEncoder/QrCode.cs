using Gma.QrCodeNet.Encoding.Common;

namespace Gma.QrCodeNet.Encoding
{
	/// <summary>
	/// This class contain two variables. 
	/// BitMatrix for QrCode
	/// isContainMatrix for indicate whether QrCode contains BitMatrix or not.
	/// BitMatrix will equal to null if isContainMatrix is false. 
	/// </summary>
    public class QrCode
    {
        internal QrCode(BitMatrix matrix)
        {
			Matrix = matrix;
			isContainMatrix = true;
        }
        
        public QrCode()
        {
			isContainMatrix = false;
			Matrix = null;
        }
        
        public bool isContainMatrix
        {
        	get;
        	private set;
        }

        public BitMatrix Matrix
        {
            get;
            private set;
        }
    }
}