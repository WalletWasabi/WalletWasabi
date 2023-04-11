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

using ZXing.Common.Test;

namespace ZXing.Datamatrix.Test
{
    /// <summary>
    /// <author>gitlost</author>
    /// </summary>
    public sealed class DataMatrixBlackBox3TestCase : AbstractBlackBoxTestCase
    {
        public DataMatrixBlackBox3TestCase()
           : base("test/data/blackbox/datamatrix-3", new MultiFormatReader(), BarcodeFormat.DATA_MATRIX)
        {
            addTest(18, 18, 0.0f);
            addTest(17, 17, 90.0f);
            addTest(18, 18, 180.0f);
            addTest(18, 18, 270.0f);
        }
    }
}
