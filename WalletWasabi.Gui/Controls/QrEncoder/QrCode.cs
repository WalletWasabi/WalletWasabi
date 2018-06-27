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
        	this.Matrix = matrix;
        	this.isContainMatrix = true;
        }
        
        public QrCode()
        {
        	this.isContainMatrix = false;
        	this.Matrix = null;
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