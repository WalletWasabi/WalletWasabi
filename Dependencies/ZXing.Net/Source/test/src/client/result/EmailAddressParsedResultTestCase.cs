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
   /// Tests <see cref="EmailAddressParsedResult" />.
   ///
   /// <author>Sean Owen</author>
   /// </summary>
   [TestFixture]
   public sealed class EmailAddressParsedResultTestCase
   {
      [Test]
      public void testEmailAddress()
      {
         doTest("srowen@example.org", "srowen@example.org", null, null);
         doTest("mailto:srowen@example.org", "srowen@example.org", null, null);
      }

      [Test]
      public void testTos()
      {
         doTest("mailto:srowen@example.org,bob@example.org",
                new String[] {"srowen@example.org", "bob@example.org"},
                null, null, null, null);
         doTest("mailto:?to=srowen@example.org,bob@example.org",
                new String[] {"srowen@example.org", "bob@example.org"},
                null, null, null, null);
      }

      [Test]
      public void testCCs()
      {
         doTest("mailto:?cc=srowen@example.org",
                null,
                new String[] {"srowen@example.org"},
                null, null, null);
         doTest("mailto:?cc=srowen@example.org,bob@example.org",
                null,
                new String[] {"srowen@example.org", "bob@example.org"},
                null, null, null);
      }

      [Test]
      public void testBCCs()
      {
         doTest("mailto:?bcc=srowen@example.org",
                null, null,
                new String[] {"srowen@example.org"},
                null, null);
         doTest("mailto:?bcc=srowen@example.org,bob@example.org",
                null, null,
                new String[] {"srowen@example.org", "bob@example.org"},
                null, null);
      }

      [Test]
      public void testAll()
      {
         doTest("mailto:bob@example.org?cc=foo@example.org&bcc=srowen@example.org&subject=baz&body=buzz",
                new String[] {"bob@example.org"},
                new String[] {"foo@example.org"},
                new String[] {"srowen@example.org"},
                "baz",
                "buzz");
      }

      [Test]
      public void testEmailDocomo()
      {
         doTest("MATMSG:TO:srowen@example.org;;", "srowen@example.org", null, null);
         doTest("MATMSG:TO:srowen@example.org;SUB:Stuff;;", "srowen@example.org", "Stuff", null);
         doTest("MATMSG:TO:srowen@example.org;SUB:Stuff;BODY:This is some text;;", "srowen@example.org",
                "Stuff", "This is some text");
      }

      [Test]
      public void testSMTP()
      {
         doTest("smtp:srowen@example.org", "srowen@example.org", null, null);
         doTest("SMTP:srowen@example.org", "srowen@example.org", null, null);
         doTest("smtp:srowen@example.org:foo", "srowen@example.org", "foo", null);
         doTest("smtp:srowen@example.org:foo:bar", "srowen@example.org", "foo", "bar");
      }

      private static void doTest(String contents,
                                 String to,
                                 String subject,
                                 String body)
      {
         doTest(contents, new String[] {to}, null, null, subject, body);
      }

      private static void doTest(String contents,
                                 String[] tos,
                                 String[] ccs,
                                 String[] bccs,
                                 String subject,
                                 String body)
      {
         var fakeResult = new ZXing.Result(contents, null, null, BarcodeFormat.QR_CODE);
         var result = ResultParser.parseResult(fakeResult);
         Assert.AreEqual(ParsedResultType.EMAIL_ADDRESS, result.Type);
         var emailResult = (EmailAddressParsedResult)result;
         Assert.AreEqual(tos, emailResult.Tos);
         Assert.AreEqual(ccs, emailResult.CCs);
         Assert.AreEqual(bccs, emailResult.BCCs);
         Assert.AreEqual(subject, emailResult.Subject);
         Assert.AreEqual(body, emailResult.Body);
      }
   }
}