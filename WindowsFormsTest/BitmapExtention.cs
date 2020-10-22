using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Emgu.CV.Cuda;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace Emgu.CV
{
	/// <summary>
	/// Provide extension method to convert IInputArray to and from Bitmap
	/// </summary>
	public static class BitmapExtension
	{
		#region Color Palette

		/// <summary>
		/// The ColorPalette of Grayscale for Bitmap Format8bppIndexed
		/// </summary>
		public static readonly ColorPalette GrayscalePalette = GenerateGrayscalePalette();

		private static ColorPalette GenerateGrayscalePalette()
		{
			using Bitmap image = new Bitmap(1, 1, PixelFormat.Format8bppIndexed);
			ColorPalette palette = image.Palette;
			for (int i = 0; i < 256; i++)
			{
				palette.Entries[i] = Color.FromArgb(i, i, i);
			}

			return palette;
		}

		/// <summary>
		/// Convert the color palette to four lookup tables
		/// </summary>
		/// <param name="palette">The color palette to transform</param>
		/// <param name="bTable">Lookup table for the B channel</param>
		/// <param name="gTable">Lookup table for the G channel</param>
		/// <param name="rTable">Lookup table for the R channel</param>
		/// <param name="aTable">Lookup table for the A channel</param>
		public static void ColorPaletteToLookupTable(ColorPalette palette, out Matrix<Byte> bTable,
			out Matrix<Byte> gTable, out Matrix<Byte> rTable, out Matrix<Byte> aTable)
		{
			bTable = new Matrix<byte>(256, 1);
			gTable = new Matrix<byte>(256, 1);
			rTable = new Matrix<byte>(256, 1);
			aTable = new Matrix<byte>(256, 1);
			byte[,] bData = bTable.Data;
			byte[,] gData = gTable.Data;
			byte[,] rData = rTable.Data;
			byte[,] aData = aTable.Data;

			Color[] colors = palette.Entries;
			for (int i = 0; i < colors.Length; i++)
			{
				Color c = colors[i];
				bData[i, 0] = c.B;
				gData[i, 0] = c.G;
				rData[i, 0] = c.R;
				aData[i, 0] = c.A;
			}
		}

		#endregion Color Palette

		/// <summary>
		/// Convert raw data to bitmap
		/// </summary>
		/// <param name="scan0">The pointer to the raw data</param>
		/// <param name="step">The step</param>
		/// <param name="size">The size of the image</param>
		/// <param name="srcColorType">The source image color type</param>
		/// <param name="numberOfChannels">The number of channels</param>
		/// <param name="srcDepthType">The source image depth type</param>
		/// <param name="tryDataSharing">Try to create Bitmap that shares the data with the image</param>
		/// <returns>A bitmap representation of the image.</returns>
		public static Bitmap RawDataToBitmap(IntPtr scan0, int step, Size size, Type srcColorType, int numberOfChannels,
			Type srcDepthType, bool tryDataSharing = false)
		{
			if (tryDataSharing)
			{
				if (srcColorType == typeof(Gray) && srcDepthType == typeof(Byte))
				{
					//Grayscale of Bytes
					Bitmap bmpGray = new Bitmap(
						size.Width,
						size.Height,
						step,
						System.Drawing.Imaging.PixelFormat.Format8bppIndexed,
						scan0
					);

					bmpGray.Palette = GrayscalePalette;

					return bmpGray;
				}
				// Mono in Linux doesn't support scan0 constructor with Format24bppRgb, use ToBitmap instead
				// See https://bugzilla.novell.com/show_bug.cgi?id=363431
				// TODO: check mono buzilla Bug 363431 to see when it will be fixed
				else if (
					Emgu.Util.Platform.OperationSystem == Emgu.Util.Platform.OS.Windows &&
					Emgu.Util.Platform.ClrType == Emgu.Util.Platform.Clr.DotNet &&
					srcColorType == typeof(Bgr) && srcDepthType == typeof(Byte)
					&& (step & 3) == 0)
				{
					//Bgr byte
					return new Bitmap(
						size.Width,
						size.Height,
						step,
						System.Drawing.Imaging.PixelFormat.Format24bppRgb,
						scan0);
				}
				else if (srcColorType == typeof(Bgra) && srcDepthType == typeof(Byte))
				{
					//Bgra byte
					return new Bitmap(
						size.Width,
						size.Height,
						step,
						System.Drawing.Imaging.PixelFormat.Format32bppArgb,
						scan0);
				}

				//PixelFormat.Format16bppGrayScale is not supported in .NET
				//else if (typeof(TColor) == typeof(Gray) && typeof(TDepth) == typeof(UInt16))
				//{
				//   return new Bitmap(
				//      size.width,
				//      size.height,
				//      step,
				//      PixelFormat.Format16bppGrayScale;
				//      scan0);
				//}
			}

			System.Drawing.Imaging.PixelFormat format; //= System.Drawing.Imaging.PixelFormat.Undefined;

			if (srcColorType == typeof(Gray)) // if this is a gray scale image
			{
				format = System.Drawing.Imaging.PixelFormat.Format8bppIndexed;
			}
			else if (srcColorType == typeof(Bgra)) //if this is Bgra image
			{
				format = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
			}
			else if (srcColorType == typeof(Bgr)) //if this is a Bgr Byte image
			{
				format = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
			}
			else
			{
				using Mat m = new Mat(size.Height, size.Width, CvInvoke.GetDepthType(srcDepthType), numberOfChannels,
					scan0, step);
				using Mat m2 = new Mat();
				CvInvoke.CvtColor(m, m2, srcColorType, typeof(Bgr));
				return RawDataToBitmap(m2.DataPointer, m2.Step, m2.Size, typeof(Bgr), 3, srcDepthType, false);
			}

			Bitmap bmp = new Bitmap(size.Width, size.Height, format);
			System.Drawing.Imaging.BitmapData data = bmp.LockBits(
				new Rectangle(Point.Empty, size),
				System.Drawing.Imaging.ImageLockMode.WriteOnly,
				format);
			using (Mat bmpMat = new Mat(size.Height, size.Width, CvEnum.DepthType.Cv8U, numberOfChannels, data.Scan0,
				data.Stride))
			using (Mat dataMat = new Mat(size.Height, size.Width, CvInvoke.GetDepthType(srcDepthType), numberOfChannels,
				scan0, step))
			{
				if (srcDepthType == typeof(Byte))
				{
					dataMat.CopyTo(bmpMat);
				}
				else
				{
					double scale = 1.0, shift = 0.0;
					RangeF range = dataMat.GetValueRange();
					if (range.Max > 255.0 || range.Min < 0)
					{
						scale = range.Max.Equals(range.Min) ? 0.0 : 255.0 / (range.Max - range.Min);
						shift = scale.Equals(0) ? range.Min : -range.Min * scale;
					}

					CvInvoke.ConvertScaleAbs(dataMat, bmpMat, scale, shift);
				}
			}

			bmp.UnlockBits(data);

			if (format == System.Drawing.Imaging.PixelFormat.Format8bppIndexed)
			{
				bmp.Palette = GrayscalePalette;
			}

			return bmp;
		}

		/// <summary>
		/// Convert the mat into Bitmap, the pixel values are copied over to the Bitmap
		/// </summary>
		/// <param name="mat">The Mat to be converted to Bitmap</param>
		/// <returns>A bitmap representation of the image.</returns>
		public static Bitmap ToBitmap(this Mat mat)
		{
			if (mat.Dims > 3)
			{
				return null;
			}

			int channels = mat.NumberOfChannels;
			Size s = mat.Size;
			Type colorType;
			switch (channels)
			{
				case 1:
					colorType = typeof(Gray);

					if (s.Equals(Size.Empty))
					{
						return null;
					}

					if ((s.Width | 3) != 0) //handle the special case where width is not a multiple of 4
					{
						Bitmap bmp = new Bitmap(s.Width, s.Height, PixelFormat.Format8bppIndexed);
						bmp.Palette = GrayscalePalette;
						BitmapData bitmapData = bmp.LockBits(new Rectangle(Point.Empty, s), ImageLockMode.WriteOnly,
							PixelFormat.Format8bppIndexed);
						using (Mat m = new Mat(s.Height, s.Width, DepthType.Cv8U, 1, bitmapData.Scan0,
							bitmapData.Stride))
						{
							mat.CopyTo(m);
						}

						bmp.UnlockBits(bitmapData);
						return bmp;
					}

					break;

				case 3:
					colorType = typeof(Bgr);
					break;

				case 4:
					colorType = typeof(Bgra);
					break;

				default:
					throw new Exception("Unknown color type");
			}

			return RawDataToBitmap(mat.DataPointer, mat.Step, s, colorType, mat.NumberOfChannels,
				CvInvoke.GetDepthType(mat.Depth), true);
		}

		/// <summary>
		/// Convert the umat into Bitmap, the pixel values are copied over to the Bitmap
		/// </summary>
		/// <param name="umat">The UMat to be converted to Bitmap</param>
		public static Bitmap ToBitmap(this UMat umat)
		{
			using Mat tmp = umat.GetMat(CvEnum.AccessType.Read);
			return tmp.ToBitmap();
		}

		/// <summary>
		/// Convert the gpuMat into Bitmap, the pixel values are copied over to the Bitmap
		/// </summary>
		/// <param name="gpuMat">The gpu mat to be converted to Bitmap</param>
		/// <returns>A bitmap representation of the image.</returns>
		public static Bitmap ToBitmap(this GpuMat gpuMat)
		{
			using Mat tmp = new Mat();
			gpuMat.Download(tmp);
			return tmp.ToBitmap();
		}

		/// <summary>
		/// Create an Image &lt; TColor, TDepth &gt; from Bitmap
		/// </summary>
		/// <param name="bitmap">The Bitmap to be converted to Image &lt; TColor, TDepth &gt;</param>
		/// <typeparam name="TColor">The color type of the Image</typeparam>
		/// <typeparam name="TDepth">The depth type of the Image</typeparam>
		/// <returns>The Image &lt; TColor, TDepth &gt; converted from Bitmap</returns>
		public static Image<TColor, TDepth> ToImage<TColor, TDepth>(this Bitmap bitmap) where
			TColor : struct, IColor
			where TDepth : new()
		{
			Size size = bitmap.Size;
			Image<TColor, TDepth> image = new Image<TColor, TDepth>(size);

			switch (bitmap.PixelFormat)
			{
				case PixelFormat.Format32bppRgb:
					if (typeof(TColor) == typeof(Bgr) && typeof(TDepth) == typeof(Byte))
					{
						BitmapData data = bitmap.LockBits(
							new Rectangle(Point.Empty, size),
							ImageLockMode.ReadOnly,
							bitmap.PixelFormat);

						using (Image<Bgra, Byte> mat =
							new Image<Bgra, Byte>(size.Width, size.Height, data.Stride, data.Scan0))
						{
							CvInvoke.MixChannels(mat, image, new[] { 0, 0, 1, 1, 2, 2 });
						}

						bitmap.UnlockBits(data);
					}
					else
					{
						using Image<Bgr, Byte> tmp = bitmap.ToImage<Bgr, byte>();
						image.ConvertFrom(tmp);
					}

					break;

				case PixelFormat.Format32bppArgb:
					if (typeof(TColor) == typeof(Bgra) && typeof(TDepth) == typeof(Byte))
					{
						image.CopyFromBitmap(bitmap);
					}
					else
					{
						BitmapData data = bitmap.LockBits(
							new Rectangle(Point.Empty, size),
							ImageLockMode.ReadOnly,
							bitmap.PixelFormat);
						using (Image<Bgra, Byte> tmp =
							new Image<Bgra, byte>(size.Width, size.Height, data.Stride, data.Scan0))
							image.ConvertFrom(tmp);
						bitmap.UnlockBits(data);
					}

					break;

				case PixelFormat.Format8bppIndexed:
					if (typeof(TColor) == typeof(Bgra) && typeof(TDepth) == typeof(Byte))
					{
						Matrix<Byte> bTable, gTable, rTable, aTable;
						ColorPaletteToLookupTable(bitmap.Palette, out bTable, out gTable, out rTable, out aTable);
						BitmapData data = bitmap.LockBits(
							new Rectangle(Point.Empty, size),
							ImageLockMode.ReadOnly,
							bitmap.PixelFormat);
						using (Image<Gray, Byte> indexValue =
							new Image<Gray, byte>(size.Width, size.Height, data.Stride, data.Scan0))
						{
							using Mat b = new Mat();
							using Mat g = new Mat();
							using Mat r = new Mat();
							using Mat a = new Mat();
							CvInvoke.LUT(indexValue, bTable, b);
							CvInvoke.LUT(indexValue, gTable, g);
							CvInvoke.LUT(indexValue, rTable, r);
							CvInvoke.LUT(indexValue, aTable, a);
							using VectorOfMat mv = new VectorOfMat(new Mat[] { b, g, r, a });
							CvInvoke.Merge(mv, image);
						}

						bitmap.UnlockBits(data);
						bTable.Dispose();
						gTable.Dispose();
						rTable.Dispose();
						aTable.Dispose();
					}
					else
					{
						using Image<Bgra, Byte> tmp = bitmap.ToImage<Bgra, Byte>();
						image.ConvertFrom(tmp);
					}

					break;

				case PixelFormat.Format24bppRgb:
					if (typeof(TColor) == typeof(Bgr) && typeof(TDepth) == typeof(Byte))
					{
						image.CopyFromBitmap(bitmap);
					}
					else
					{
						BitmapData data = bitmap.LockBits(
							new Rectangle(Point.Empty, size),
							ImageLockMode.ReadOnly,
							bitmap.PixelFormat);
						using (Image<Bgr, Byte> tmp =
							new Image<Bgr, byte>(size.Width, size.Height, data.Stride, data.Scan0))
							image.ConvertFrom(tmp);
						bitmap.UnlockBits(data);
					}

					break;

				case PixelFormat.Format1bppIndexed:
					if (typeof(TColor) == typeof(Gray) && typeof(TDepth) == typeof(Byte))
					{
						int rows = size.Height;
						int cols = size.Width;
						BitmapData data = bitmap.LockBits(
							new Rectangle(Point.Empty, size),
							ImageLockMode.ReadOnly,
							bitmap.PixelFormat);

						int fullByteCount = cols >> 3;
						int partialBitCount = cols & 7;

						int mask = 1 << 7;

						Int64 srcAddress = data.Scan0.ToInt64();
						Byte[,,] imagedata = image.Data as Byte[,,];

						Byte[] row = new byte[fullByteCount + (partialBitCount == 0 ? 0 : 1)];

						int v = 0;
						for (int i = 0; i < rows; i++, srcAddress += data.Stride)
						{
							Marshal.Copy((IntPtr)srcAddress, row, 0, row.Length);

							for (int j = 0; j < cols; j++, v <<= 1)
							{
								if ((j & 7) == 0)
								{
									//fetch the next byte
									v = row[j >> 3];
								}

								imagedata[i, j, 0] = (v & mask) == 0 ? (Byte)0 : (Byte)255;
							}
						}
					}
					else
					{
						using (Image<Gray, Byte> tmp = bitmap.ToImage<Gray, Byte>())
							image.ConvertFrom(tmp);
					}

					break;

				default:

					#region Handle other image type

					//         Bitmap bgraImage = new Bitmap(value.Width, value.Height, PixelFormat.Format32bppArgb);
					//         using (Graphics g = Graphics.FromImage(bgraImage))
					//         {
					//            g.DrawImageUnscaled(value, 0, 0, value.Width, value.Height);
					//         }
					//         Bitmap = bgraImage;
					using (Image<Bgra, Byte> tmp1 = new Image<Bgra, Byte>(size))
					{
						Byte[,,] data = tmp1.Data;
						for (int i = 0; i < size.Width; i++)
							for (int j = 0; j < size.Height; j++)
							{
								Color color = bitmap.GetPixel(i, j);
								data[j, i, 0] = color.B;
								data[j, i, 1] = color.G;
								data[j, i, 2] = color.R;
								data[j, i, 3] = color.A;
							}

						image.ConvertFrom<Bgra, Byte>(tmp1);
					}

					#endregion Handle other image type

					break;
			}

			return image;
		}

		/// <summary>
		/// Utility function for converting Bitmap to Image
		/// </summary>
		/// <param name="bmp">the bitmap to copy data from</param>
		/// <param name="image">The image to copy data to</param>
		/// <typeparam name="TColor">The color type of the Image</typeparam>
		/// <typeparam name="TDepth">The depth type of the Image</typeparam>
		private static void CopyFromBitmap<TColor, TDepth>(this Image<TColor, TDepth> image, Bitmap bmp) where
			TColor : struct, IColor
			where TDepth : new()
		{
			BitmapData data = bmp.LockBits(
				new Rectangle(Point.Empty, bmp.Size),
				ImageLockMode.ReadOnly,
				bmp.PixelFormat);

			using (Matrix<TDepth> mat =
				new Matrix<TDepth>(bmp.Height, bmp.Width, image.NumberOfChannels, data.Scan0, data.Stride))
				CvInvoke.cvCopy(mat.Ptr, image.Ptr, IntPtr.Zero);

			bmp.UnlockBits(data);
		}

		/// <summary>
		/// Provide a more efficient way to convert Image&lt;Gray, Byte&gt;, Image&lt;Bgr, Byte&gt; and Image&lt;Bgra, Byte&gt; into Bitmap
		/// such that the image data is <b>shared</b> with Bitmap.
		/// If you change the pixel value on the Bitmap, you change the pixel values on the Image object as well!
		/// For other types of image this property has the same effect as ToBitmap()
		/// <b>Take extra caution not to use the Bitmap after the Image object is disposed</b>
		/// </summary>
		/// <typeparam name="TColor">The color of the image</typeparam>
		/// <typeparam name="TDepth">The depth of the image</typeparam>
		/// <param name="image">The image to create Bitmap from</param>
		/// <returns>A bitmap representation of the image. In the cases of Image&lt;Gray, Byte&gt;, Image&lt;Bgr, Byte&gt; and Image&lt;Bgra, Byte&gt;, the image data is shared between the Bitmap and the Image object.</returns>
		public static Bitmap AsBitmap<TColor, TDepth>(this Image<TColor, TDepth> image) where
			TColor : struct, IColor
			where TDepth : new()
		{
			IntPtr scan0;
			int step;
			Size size;
			CvInvoke.cvGetRawData(image.Ptr, out scan0, out step, out size);

			return RawDataToBitmap(scan0, step, size, typeof(TColor), image.NumberOfChannels, typeof(TDepth), true);
		}

		/// <summary>
		/// Convert this image into Bitmap, the pixel values are copied over to the Bitmap
		/// </summary>
		/// <typeparam name="TColor">The color type of the Image</typeparam>
		/// <typeparam name="TDepth">The depth type of the Image</typeparam>
		/// <param name="image">The image to be converted to Bitmap</param>
		/// <remarks> For better performance on Image&lt;Gray, Byte&gt; and Image&lt;Bgr, Byte&gt;, consider using the Bitmap property </remarks>
		/// <returns> This image in Bitmap format, the pixel data are copied over to the Bitmap</returns>
		public static Bitmap ToBitmap<TColor, TDepth>(this Image<TColor, TDepth> image) where
			TColor : struct, IColor
			where TDepth : new()
		{
			Type typeOfColor = typeof(TColor);
			Type typeofDepth = typeof(TDepth);

			PixelFormat format = PixelFormat.Undefined;

			if (typeOfColor == typeof(Gray)) // if this is a gray scale image
			{
				format = PixelFormat.Format8bppIndexed;
			}
			else if (typeOfColor == typeof(Bgra)) //if this is Bgra image
			{
				format = PixelFormat.Format32bppArgb;
			}
			else if (typeOfColor == typeof(Bgr)) //if this is a Bgr Byte image
			{
				format = PixelFormat.Format24bppRgb;
			}
			else
			{
				using (Image<Bgr, Byte> temp = image.Convert<Bgr, Byte>())
					return ToBitmap<Bgr, Byte>(temp);
			}

			if (typeof(TDepth) == typeof(Byte))
			{
				Size size = image.Size;
				Bitmap bmp = new Bitmap(size.Width, size.Height, format);
				BitmapData data = bmp.LockBits(
					new Rectangle(Point.Empty, size),
					ImageLockMode.WriteOnly,
					format);
				//using (Matrix<Byte> m = new Matrix<byte>(size.Height, size.Width, data.Scan0, data.Stride))
				using (Mat mat = new Mat(size.Height, size.Width, CV.CvEnum.DepthType.Cv8U, image.NumberOfChannels,
					data.Scan0, data.Stride))
				{
					image.Mat.CopyTo(mat);
				}

				bmp.UnlockBits(data);

				if (format == PixelFormat.Format8bppIndexed)
				{
					bmp.Palette = GrayscalePalette;
				}

				return bmp;
			}
			else
			{
				using (Image<TColor, Byte> temp = image.Convert<TColor, Byte>())
					return temp.ToBitmap();
			}
		}

		/// <summary> Create a Bitmap image of certain size</summary>
		/// <param name="image">The image to be converted to Bitmap</param>
		/// <param name="width">The width of the bitmap</param>
		/// <param name="height"> The height of the bitmap</param>
		/// <typeparam name="TColor">The color type of the Image</typeparam>
		/// <typeparam name="TDepth">The depth type of the Image</typeparam>
		/// <returns> This image in Bitmap format of the specific size</returns>
		public static Bitmap ToBitmap<TColor, TDepth>(this Image<TColor, TDepth> image, int width, int height) where
			TColor : struct, IColor
			where TDepth : new()
		{
			using (Image<TColor, TDepth> scaledImage = image.Resize(width, height, CvEnum.Inter.Linear))
				return scaledImage.ToBitmap();
		}

		/// <summary>
		/// Convert the CudaImage to its equivalent Bitmap representation
		/// </summary>
		/// <param name="cudaImage">The cuda image to be converted to Bitmap</param>
		/// <typeparam name="TColor">The color type of the CudaImage</typeparam>
		/// <typeparam name="TDepth">The depth type of the CudaImage</typeparam>
		/// <returns> This image in Bitmap format, the pixel data are copied over to the Bitmap</returns>
		public static Bitmap ToBitmap<TColor, TDepth>(this CudaImage<TColor, TDepth> cudaImage) where
			TColor : struct, IColor
			where TDepth : new()
		{
			if (typeof(TColor) == typeof(Bgr) && typeof(TDepth) == typeof(Byte))
			{
				Size s = cudaImage.Size;
				Bitmap result = new Bitmap(s.Width, s.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
				System.Drawing.Imaging.BitmapData data = result.LockBits(new Rectangle(Point.Empty, result.Size),
					System.Drawing.Imaging.ImageLockMode.WriteOnly, result.PixelFormat);
				using (Image<TColor, TDepth> tmp = new Image<TColor, TDepth>(s.Width, s.Height, data.Stride, data.Scan0)
				)
				{
					cudaImage.Download(tmp);
				}

				result.UnlockBits(data);
				return result;
			}
			else
			{
				using (Image<TColor, TDepth> tmp = cudaImage.ToImage())
				{
					return tmp.ToBitmap();
				}
			}
		}
	}

	/// <summary>
	/// Class that can be used to read file into Mat
	/// </summary>
	public class BitmapFileReaderMat : Emgu.CV.IFileReaderMat
	{
		/// <summary>
		/// Read the file into a Mat
		/// </summary>
		/// <param name="fileName">The name of the image file</param>
		/// <param name="mat">The Mat to read into</param>
		/// <param name="loadType">Image load type.</param>
		/// <returns>True if the file can be read into the Mat</returns>
		public bool ReadFile(String fileName, Mat mat, CvEnum.ImreadModes loadType)
		{
			try
			{
				using (Bitmap bmp = new Bitmap(fileName))
				using (Image<Bgr, Byte> image = bmp.ToImage<Bgr, Byte>())
					image.Mat.CopyTo(mat);
				return true;
			}
			catch (Exception e)
			{
				Debug.WriteLine(e);
				//throw;
				return false;
			}
		}
	}

	/// <summary>
	/// Class that can be used to write the Mat to a file
	/// </summary>
	public class BitmapFileWriterMat : Emgu.CV.IFileWriterMat
	{
		/// <summary>
		/// Write the Mat into the file
		/// </summary>
		/// <param name="mat">The Mat to write</param>
		/// <param name="fileName">The name of the file to be written into</param>
		/// <returns>True if the file has been written into Mat</returns>
		public bool WriteFile(Mat mat, String fileName)
		{
			try
			{
				//Try to save the image using .NET's Bitmap class
				String extension = System.IO.Path.GetExtension(fileName);
				if (!String.IsNullOrEmpty(extension))
				{
					using (Bitmap bmp = mat.ToBitmap())
					{
						switch (extension.ToLower())
						{
							case ".jpg":
							case ".jpeg":
								bmp.Save(fileName, ImageFormat.Jpeg);
								break;

							case ".bmp":
								bmp.Save(fileName, ImageFormat.Bmp);
								break;

							case ".png":
								bmp.Save(fileName, ImageFormat.Png);
								break;

							case ".tiff":
							case ".tif":
								bmp.Save(fileName, ImageFormat.Tiff);
								break;

							case ".gif":
								bmp.Save(fileName, ImageFormat.Gif);
								break;

							default:
								throw new NotImplementedException(String.Format("Saving to {0} format is not supported", extension));
						}
					}
				}

				return true;
			}
			catch (Exception e)
			{
				Debug.WriteLine(e);
				//throw;
				return false;
			}
		}
	}
}
