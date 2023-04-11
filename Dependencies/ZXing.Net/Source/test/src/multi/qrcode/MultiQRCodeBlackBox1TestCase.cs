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
using System.Drawing;
using System.IO;
using NUnit.Framework;
using ZXing.Common;
using ZXing.Common.Test;

namespace ZXing.Multi.QrCode.Test
{
    /// <summary>
    /// <author>Sean Owen</author>
    /// </summary>
    public sealed class MultiQRCodeBlackBox1TestCase : AbstractBlackBoxTestCase
    {
        public MultiQRCodeBlackBox1TestCase()
            : base("test/data/blackbox/multi-qrcode-1", new QRCodeMultiReader(), BarcodeFormat.QR_CODE)
        {
            addTest(2, 2, 0.0f);
            addTest(2, 2, 90.0f);
            addTest(2, 2, 180.0f);
            addTest(2, 2, 270.0f);
        }

        [Test]
        public void testMultiQRCodes()
        {
            var path = buildTestBase("test/data/blackbox/multi-qrcode-1");
            var source = new BitmapLuminanceSource((Bitmap) Bitmap.FromFile(Path.Combine(path, "1.png")));
            var bitmap = new BinaryBitmap(new HybridBinarizer(source));

            var reader = new QRCodeMultiReader();
            var results = reader.decodeMultiple(bitmap);
            Assert.IsNotNull(results);
            Assert.AreEqual(4, results.Length);

            var barcodeContents = new HashSet<String>();
            foreach (Result result in results)
            {
                barcodeContents.Add(result.Text);
                Assert.AreEqual(BarcodeFormat.QR_CODE, result.BarcodeFormat);
                var metadata = result.ResultMetadata;
                Assert.IsNotNull(metadata);
            }

            var expectedContents = new HashSet<String>
            {
                "You earned the class a 5 MINUTE DANCE PARTY!!  Awesome!  Way to go!  Let's boogie!",
                "You earned the class 5 EXTRA MINUTES OF RECESS!!  Fabulous!!  Way to go!!",
                "You get to SIT AT MRS. SIGMON'S DESK FOR A DAY!!  Awesome!!  Way to go!! Guess I better clean up! :)",
                "You get to CREATE OUR JOURNAL PROMPT FOR THE DAY!  Yay!  Way to go!  "
            };

            foreach (var expected in expectedContents)
            {
                Assert.That(barcodeContents.Contains(expected), Is.True);
            }
        }

        [Test]
        public void testProcessStructuredAppend()
        {
            var sa1 = new Result("SA1", new byte[] { }, new ResultPoint[] { }, BarcodeFormat.QR_CODE);
            var sa2 = new Result("SA2", new byte[] { }, new ResultPoint[] { }, BarcodeFormat.QR_CODE);
            var sa3 = new Result("SA3", new byte[] { }, new ResultPoint[] { }, BarcodeFormat.QR_CODE);
            sa1.putMetadata(ResultMetadataType.STRUCTURED_APPEND_SEQUENCE, (0 << 4) + 2);
            sa1.putMetadata(ResultMetadataType.ERROR_CORRECTION_LEVEL, "L");
            sa2.putMetadata(ResultMetadataType.STRUCTURED_APPEND_SEQUENCE, (1 << 4) + 2);
            sa2.putMetadata(ResultMetadataType.ERROR_CORRECTION_LEVEL, "L");
            sa3.putMetadata(ResultMetadataType.STRUCTURED_APPEND_SEQUENCE, (2 << 4) + 2);
            sa3.putMetadata(ResultMetadataType.ERROR_CORRECTION_LEVEL, "L");

            var nsa = new Result("NotSA", new byte[] { }, new ResultPoint[] { }, BarcodeFormat.QR_CODE);
            nsa.putMetadata(ResultMetadataType.ERROR_CORRECTION_LEVEL, "L");

            var inputs = new List<Result> {sa3, sa1, nsa, sa2};

            var results = QRCodeMultiReader.ProcessStructuredAppend(inputs);
            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(2));

            var barcodeContents = new HashSet<string>();
            foreach (Result result in results)
            {
                barcodeContents.Add(result.Text);
            }
            var expectedContents = new HashSet<string>();
            expectedContents.Add("NotSA");
            expectedContents.Add("SA1SA2SA3");
            Assert.That(barcodeContents, Is.EqualTo(expectedContents));
        }
    }
}
