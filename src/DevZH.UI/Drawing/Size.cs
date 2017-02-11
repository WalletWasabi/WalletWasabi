using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Drawing
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Size
    {
        public double _width;
        public double _height;

        public Size(double width, double height)
        {
            if (width < 0 || height < 0)
            {
                throw new ArgumentException("Size cannot be negative");
            }

            _width = width;
            _height = height;
        }

        public double Width
        {
            get { return _width; }
            set
            {
                if (IsEmpty)
                {
                    throw new InvalidOperationException("cannot modify the empty size");
                }

                if (value < 0)
                {
                    throw new ArgumentException("Size width cannot be nagative");
                }

                _width = value;
            }
        }

        public double Height
        {
            get { return _height; }
            set
            {
                if (IsEmpty)
                {
                    throw new InvalidOperationException("cannot modify the empty size");
                }

                if (value < 0)
                {
                    throw new ArgumentException("Size height cannot be nagative");
                }

                _height = value;
            }
        }

        public bool IsEmpty => Width < 0;

        public static bool operator ==(Size size1, Size size2)
        {
            return size1.Width == size2.Width &&
                   size1.Height == size2.Height;
        }

        public static bool operator !=(Size size1, Size size2)
        {
            return !(size1 == size2);
        }

        public static bool Equals(Size size1, Size size2)
        {
            if (size1.IsEmpty)
            {
                return size2.IsEmpty;
            }
            else
            {
                return size1.Width.Equals(size2.Width) &&
                       size1.Height.Equals(size2.Height);
            }
        }

        public override bool Equals(object o)
        {
            if (!(o is Size))
            {
                return false;
            }

            Size value = (Size)o;
            return Size.Equals(this, value);
        }

        public bool Equals(Size value)
        {
            return Size.Equals(this, value);
        }

        public override int GetHashCode()
        {
            if (IsEmpty)
            {
                return 0;
            }
            else
            {
                // Perform field-by-field XOR of HashCodes
                return Width.GetHashCode() ^
                       Height.GetHashCode();
            }
        }
    }
}
