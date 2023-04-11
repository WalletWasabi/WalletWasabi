/*
 * Copyright 2008 ZXing authors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;

using NUnit.Framework;
using ZXing.Common;
using ZXing.Datamatrix.Encoder;

namespace ZXing.Datamatrix.Test
{
    /// <summary>
    /// @author satorux@google.com (Satoru Takabayashi) - creator
    /// @author dswitkin@google.com (Daniel Switkin) - ported and expanded from C++
    /// </summary>
    [TestFixture]
    public sealed class DataMatrixWriterTestCase
    {
        [Test]
        public void testDataMatrixImageWriter()
        {
            var writer = new DataMatrixWriter();

            var hints = new Dictionary<EncodeHintType, Object>
                        {{EncodeHintType.DATA_MATRIX_SHAPE, SymbolShapeHint.FORCE_SQUARE}};

            const int bigEnough = 64;
            var matrix = writer.encode("Hello Google", BarcodeFormat.DATA_MATRIX, bigEnough, bigEnough, hints);

            Assert.IsNotNull(matrix);
            Assert.IsTrue(bigEnough >= matrix.Width);
            Assert.IsTrue(bigEnough >= matrix.Height);
        }

        [Test]
        public void testDataMatrixWriter()
        {
            var writer = new DataMatrixWriter();

            var hints = new Dictionary<EncodeHintType, Object>
                        {{EncodeHintType.DATA_MATRIX_SHAPE, SymbolShapeHint.FORCE_SQUARE}};

            const int bigEnough = 14;
            var matrix = writer.encode("Hello Me", BarcodeFormat.DATA_MATRIX, bigEnough, bigEnough, hints);

            Assert.IsNotNull(matrix);
            Assert.AreEqual(bigEnough, matrix.Width);
            Assert.AreEqual(bigEnough, matrix.Height);
        }

        [Test]
        public void testDataMatrixTooSmall()
        {
            // The DataMatrix will not fit in this size, so the matrix should come back bigger
            const int tooSmall = 8;
            var writer = new DataMatrixWriter();
            var matrix = writer.encode("http://www.google.com/", BarcodeFormat.DATA_MATRIX, tooSmall, tooSmall, null);

            Assert.IsNotNull(matrix);
            Assert.IsTrue(tooSmall < matrix.Width);
            Assert.IsTrue(tooSmall < matrix.Height);
        }

        [Test]
        public void Should_Encode_FNC1()
        {
            var content = String.Format("{0}abcdefg{0}1223456", (char)29);

            var writer = new DataMatrixWriter();

            var hints = new Dictionary<EncodeHintType, Object>();

            var matrix = writer.encode(content, BarcodeFormat.DATA_MATRIX, 1, 1, hints);

            Assert.IsNotNull(matrix);

            var reader = new DataMatrixReader();
            var readerhints = new Dictionary<DecodeHintType, Object>();
            readerhints.Add(DecodeHintType.PURE_BARCODE, true);

            var result = reader.decode(new BinaryBitmap(matrix), readerhints);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Text, Is.EqualTo(content));
        }

        [TestCase("Abc123!", SymbolShapeHint.FORCE_SQUARE)]
        [TestCase("Lorem ipsum. http://test/", SymbolShapeHint.FORCE_SQUARE)]
        [TestCase("AAAANAAAANAAAANAAAANAAAANAAAANAAAANAAAANAAAANAAAAN", SymbolShapeHint.FORCE_SQUARE)]
        [TestCase("http://test/~!@#*^%&)__ ;:'\"[]{}\\|-+-=`1029384", SymbolShapeHint.FORCE_SQUARE)]
        [TestCase(@"http://test/~!@#*^%&)__ ;:'\""[]{}\\|-+-=`1029384756<>/?abc
Four score and seven our forefathers brought forth", SymbolShapeHint.FORCE_SQUARE)]
        [TestCase(@"In ut magna vel mauris malesuada dictum. Nulla ullamcorper metus quis diam
 cursus facilisis. Sed mollis quam id justo rutrum sagittis. Donec laoreet rutrum
 est, nec convallis mauris condimentum sit amet. Phasellus gravida, justo et congue
 auctor, nisi ipsum viverra erat, eget hendrerit felis turpis nec lorem. Nulla
 ultrices, elit pellentesque aliquet laoreet, justo erat pulvinar nisi, id
 elementum sapien dolor et diam.", SymbolShapeHint.FORCE_SQUARE)]
        [TestCase(@"In ut magna vel mauris malesuada dictum. Nulla ullamcorper metus quis diam
 cursus facilisis. Sed mollis quam id justo rutrum sagittis. Donec laoreet rutrum
 est, nec convallis mauris condimentum sit amet. Phasellus gravida, justo et congue
 auctor, nisi ipsum viverra erat, eget hendrerit felis turpis nec lorem. Nulla
 ultrices, elit pellentesque aliquet laoreet, justo erat pulvinar nisi, id
 elementum sapien dolor et diam. Donec ac nunc sodales elit placerat eleifend.
 Sed ornare luctus ornare. Vestibulum vehicula, massa at pharetra fringilla, risus
 justo faucibus erat, nec porttitor nibh tellus sed est. Ut justo diam, lobortis eu
 tristique ac, p.In ut magna vel mauris malesuada dictum. Nulla ullamcorper metus
 quis diam cursus facilisis. Sed mollis quam id justo rutrum sagittis. Donec
 laoreet rutrum est, nec convallis mauris condimentum sit amet. Phasellus gravida,
 justo et congue auctor, nisi ipsum viverra erat, eget hendrerit felis turpis nec
 lorem. Nulla ultrices, elit pellentesque aliquet laoreet, justo erat pulvinar
 nisi, id elementum sapien dolor et diam. Donec ac nunc sodales elit placerat
 eleifend. Sed ornare luctus ornare. Vestibulum vehicula, massa at pharetra
 fringilla, risus justo faucibus erat, nec porttitor nibh tellus sed est. Ut justo
 diam, lobortis eu tristique ac, p. In ut magna vel mauris malesuada dictum. Nulla
 ullamcorper metus quis diam cursus facilisis. Sed mollis quam id justo rutrum
 sagittis. Donec laoreet rutrum est, nec convallis mauris condimentum sit amet.
 Phasellus gravida, justo et congue auctor, nisi ipsum viverra erat, eget hendrerit
 felis turpis nec lorem. Nulla ultrices, elit pellentesque aliquet laoreet, justo
 erat pulvinar nisi, id elementum sapien dolor et diam.", SymbolShapeHint.FORCE_SQUARE)]

        [TestCase("Abc123!", SymbolShapeHint.FORCE_RECTANGLE)]
        [TestCase("Lorem ipsum. http://test/", SymbolShapeHint.FORCE_RECTANGLE)]
        [TestCase("3i0QnD^RcZO[\\#!]1,9zIJ{1z3qrvsq", SymbolShapeHint.FORCE_RECTANGLE)]
        [TestCase("AAAANAAAANAAAANAAAANAAAANAAAANAAAANAAAANAAAANAAAAN", SymbolShapeHint.FORCE_RECTANGLE)]
        [TestCase("http://test/~!@#*^%&)__ ;:'\"[]{}\\|-+-=`1029384", SymbolShapeHint.FORCE_RECTANGLE)]
        [TestCase("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ", SymbolShapeHint.FORCE_RECTANGLE)]

        // EDIFACT, TODO: force EDIFACT
        [TestCase("https://test~[******]_", SymbolShapeHint.FORCE_NONE)]
        [TestCase("abc<->ABCDE", SymbolShapeHint.FORCE_NONE)]
        [TestCase("<ABCDEFG><ABCDEFGK>", SymbolShapeHint.FORCE_NONE)]
        [TestCase("*CH/GN1/022/00", SymbolShapeHint.FORCE_NONE)]

        [TestCase("MEMANT-1F-MESTECH", SymbolShapeHint.FORCE_NONE)]
        [TestCase("MEMANT -1F-M-2estech", SymbolShapeHint.FORCE_NONE)]
        [TestCase("MEMANT -1F-M-2e-stech", SymbolShapeHint.FORCE_NONE)]
        [TestCase("MEMANT -1F-M6ABCDEF", SymbolShapeHint.FORCE_NONE)]
        public void TestEncodeDecode(String data, SymbolShapeHint shape)
        {
            var writer = new DataMatrixWriter();
            var options = new DatamatrixEncodingOptions { SymbolShape = shape };
            var matrix = writer.encode(data, BarcodeFormat.DATA_MATRIX, 0, 0, options.Hints);

            Assert.That(matrix, Is.Not.Null);

            var res = new Internal.Decoder().decode(matrix);

            Assert.That(res, Is.Not.Null);
            Assert.That(res.Text, Is.EqualTo(data));
        }

#if !SILVERLIGHT
        [Test]
        public void TestEncodeDecodeISO88591AllChars()
        {
            var writer = new DataMatrixWriter();
            var options = new DatamatrixEncodingOptions();
            var data = new byte[256];
            for (var i = 0; i < 256; i++)
            {
                data[i] = (byte)i;
            }
            var strData = System.Text.Encoding.GetEncoding("ISO8859-1").GetString(data);
            var matrix = writer.encode(strData, BarcodeFormat.DATA_MATRIX, 0, 0, options.Hints);

            Assert.That(matrix, Is.Not.Null);

            var res = new Internal.Decoder().decode(matrix);

            Assert.That(res, Is.Not.Null);
            Assert.That(res.Text, Is.EqualTo(strData));

            var resData = System.Text.Encoding.GetEncoding("ISO8859-1").GetBytes(res.Text);
            for (var i = 0; i < 256; i++)
            {
                Assert.That(resData[i], Is.EqualTo(data[i]), i.ToString());
            }
        }

        [Test]
        public void TestEncodeDecodeISO88591AllCharsBitmap()
        {
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.DATA_MATRIX
            };
            var options = new DatamatrixEncodingOptions();
            var data = new byte[256];
            for (var i = 0; i < 256; i++)
            {
                data[i] = (byte)i;
            }
            var strData = System.Text.Encoding.GetEncoding("ISO8859-1").GetString(data);
            var bitmap = writer.Write(strData);

            Assert.That(bitmap, Is.Not.Null);

            var reader = new BarcodeReader
            {
                Options = new DecodingOptions
                {
                    PureBarcode = true
                }
            };

            var res = reader.Decode(bitmap);

            Assert.That(res, Is.Not.Null);
            Assert.That(res.Text, Is.EqualTo(strData));

            var resData = System.Text.Encoding.GetEncoding("ISO8859-1").GetBytes(res.Text);
            for (var i = 0; i < 256; i++)
            {
                Assert.That(resData[i], Is.EqualTo(data[i]), i.ToString());
            }
        }

        [TestCase("UTF-8")]
        [TestCase("ISO-8859-15")]
        [TestCase("ISO-8859-1")]
        public void test0To256AsBytesRoundTrip(string encodingStr)
        {
            var encoding = System.Text.Encoding.GetEncoding(encodingStr);
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.DATA_MATRIX,
                Options = new DatamatrixEncodingOptions
                {
                    Width = 1,
                    Height = 1,
                    CharacterSet = encodingStr
                }
            };
            var reader = new BarcodeReader
            {
                Options =
                {
                    PureBarcode = true,
                    PossibleFormats = new List<BarcodeFormat> {BarcodeFormat.DATA_MATRIX}
                }
            };
            var errors = new Dictionary<string, Exception>();
            for (int i = 0; i < 256; i++)
            {
                var content = encoding.GetString(new[] { (byte)i });
                try
                {
                    var bitmap = writer.Write(content);
                    //bitmap.Save(String.Format("D:\\test-{0}.png", encodingStr), System.Drawing.Imaging.ImageFormat.Png);
                    var result = reader.Decode(bitmap);
                    if (result == null)
                        throw new InvalidOperationException("cant be decoded");
                    Assert.That(result.Text, Is.EqualTo(content));
                }
                catch (Exception e)
                {
                    errors[content] = e;
                }
            }

            foreach (var error in errors)
            {
                var bytes = encoding.GetBytes(error.Key);
                Console.WriteLine(String.Format("Content: {0} ({1}); Error: {2}", error.Key, bytes[0], error.Value.Message));
            }

            if (errors.Count > 0)
                throw new AssertionException("not every content could be encoded and decoded");
        }

        [TestCase("UTF-8")]
        [TestCase("ISO-8859-15")]
        [TestCase("ISO-8859-1")]
        public void test0To256AsOneStringRoundTrip(string encodingStr)
        {
            var encoding = System.Text.Encoding.GetEncoding(encodingStr);
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.DATA_MATRIX,
                Options = new DatamatrixEncodingOptions
                {
                    Width = 1,
                    Height = 1,
                    CharacterSet = encodingStr
                }
            };
            var reader = new BarcodeReader
            {
                Options =
                {
                    PureBarcode = true,
                    PossibleFormats = new List<BarcodeFormat> {BarcodeFormat.DATA_MATRIX}
                }
            };
            var content = new System.Text.StringBuilder();
            for (int i = 0; i < 256; i++)
            {
                content.Append(encoding.GetString(new[] { (byte)i }));
            }
            Console.WriteLine(content.ToString());
            var bitmap = writer.Write(content.ToString());
            //bitmap.Save(String.Format("D:\\test-{0}.png", encodingStr), ImageFormat.Png);
            var result = reader.Decode(bitmap);
            if (result == null)
                throw new InvalidOperationException("cant be decoded");
            Assert.That(result.Text, Is.EqualTo(content.ToString()));
        }
#endif

        [TestCase('1')]
        [TestCase('a')]
        [TestCase('A')]
        [TestCase('�')]
        public void testCharactersForLengthUntil256RoundTrip(char character)
        {
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.DATA_MATRIX,
                Options = new DatamatrixEncodingOptions
                {
                    Width = 1,
                    Height = 1,
                    CharacterSet = "UTF-8"
                }
            };
            var reader = new BarcodeReader();
            reader.Options.PureBarcode = true;
            reader.Options.PossibleFormats = new List<BarcodeFormat>
            {
                BarcodeFormat.DATA_MATRIX
            };
            var errors = new Dictionary<string, Exception>();
            var content = new System.Text.StringBuilder();
            for (int i = 0; i < 256; i++)
            {
                content.Append(character);
                try
                {
                    var bitmap = writer.Write(content.ToString());
                    //bitmap.Save(String.Format("D:\\test-{0}-{1}.png", "UTF-8", character), System.Drawing.Imaging.ImageFormat.Png);
                    var result = reader.Decode(bitmap);
                    if (result == null)
                        throw new InvalidOperationException("cant be decoded");
                    Assert.That(result.Text, Is.EqualTo(content.ToString()));
                }
                catch (Exception e)
                {
                    errors[content.ToString()] = e;
                }
            }

            foreach (var error in errors)
            {
                Console.WriteLine(String.Format("Content: {0}; Error: {1}", error.Key, error.Value.Message));
            }

            if (errors.Count > 0)
                throw new AssertionException("not every content could be encoded and decoded");
        }
    }
}