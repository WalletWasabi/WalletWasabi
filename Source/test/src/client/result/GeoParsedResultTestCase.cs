/*
 * Copyright 2007 ZXing authors
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

using NUnit.Framework;

namespace ZXing.Client.Result.Test
{
   /// <summary>
   /// Tests <see cref="GeoParsedResult" />.
   ///
   /// <author>Sean Owen</author>
   /// </summary>
   [TestFixture]
   public sealed class GeoParsedResultTestCase
   {
      private static double EPSILON = 1.0E-10;

      [TestCase("geo:1,2", 1.0, 2.0, 0.0, null, "geo:1.0,2.0")]
      [TestCase("geo:80.33,-32.3344,3.35", 80.33, -32.3344, 3.35, null, null)]
      [TestCase("geo:-20.33,132.3344,0.01", -20.33, 132.3344, 0.01, null, null)]
      [TestCase("geo:-20.33,132.3344,0.01?q=foobar", -20.33, 132.3344, 0.01, "q=foobar", null)]
      [TestCase("GEO:-20.33,132.3344,0.01?q=foobar", -20.33, 132.3344, 0.01, "q=foobar", null)]
      public void testGeo(String contents,
                                 double latitude,
                                 double longitude,
                                 double altitude,
                                 String query,
                                 String uri)
      {
         ZXing.Result fakeResult = new ZXing.Result(contents, null, null, BarcodeFormat.QR_CODE);
         ParsedResult result = ResultParser.parseResult(fakeResult);
         Assert.AreEqual(ParsedResultType.GEO, result.Type);
         GeoParsedResult geoResult = (GeoParsedResult)result;
         Assert.AreEqual(latitude, geoResult.Latitude, EPSILON);
         Assert.AreEqual(longitude, geoResult.Longitude, EPSILON);
         Assert.AreEqual(altitude, geoResult.Altitude, EPSILON);
         Assert.AreEqual(query, geoResult.Query);
         Assert.AreEqual(uri ?? contents.ToLower(), geoResult.GeoURI);
      }
   }
}