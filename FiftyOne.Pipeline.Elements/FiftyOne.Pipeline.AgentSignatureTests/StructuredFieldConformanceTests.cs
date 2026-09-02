/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

using FiftyOne.Pipeline.AgentSignature.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace FiftyOne.Pipeline.AgentSignature.Tests
{
    /// <summary>
    /// Runs the HTTP Working Group's own structured field test suite
    /// through <c>StructuredFieldParser</c>, one test result per case. The
    /// suite is vendored under
    /// <c>Fixtures/StructuredFieldTests</c> and its provenance and licence
    /// are recorded in <c>SOURCE.txt</c> and <c>LICENSE.md</c> beside the
    /// data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Our parser implements only the part of RFC 8941 that the three Web
    /// Bot Auth headers use, so a conformance suite that now targets
    /// RFC 9651 cannot simply be asserted wholesale. Each case is therefore
    /// put into one of five buckets and judged by the rule for that bucket.
    /// The rules are as follows.
    /// </para>
    /// <para>
    /// A case whose <c>header_type</c> is 'list' is not run at all, because
    /// the element never parses a bare list. Every other case is run,
    /// through <c>TryParseDictionary</c> when the header type is
    /// 'dictionary' and through <c>TryParseItem</c> when it is 'item'.
    /// </para>
    /// <para>
    /// A case marked <c>must_fail</c> requires our parser to answer false.
    /// This is the valuable half of the suite because it catches junk we
    /// would otherwise have accepted. A case in this bucket that our parser
    /// accepts is a defect in the parser and is reported as a failure here
    /// rather than being skipped.
    /// </para>
    /// <para>
    /// A case marked <c>can_fail</c> carries a SHOULD level tolerance, so
    /// either outcome is allowed. When our parser does accept such a case
    /// the parsed value must still match the value the suite gives, because
    /// accepting something and then reading it differently would be wrong
    /// either way.
    /// </para>
    /// <para>
    /// A case that is neither <c>must_fail</c> nor <c>can_fail</c> but whose
    /// raw text needs a type we deliberately do not implement, meaning a
    /// Date written with a leading '@' or a Display String written with a
    /// leading '%' before the opening quote, must be rejected by our parser.
    /// Returning false there is the correct answer and not a failure. These
    /// cases are asserted rather than skipped, because a skip would hide the
    /// day one of them started to parse.
    /// </para>
    /// <para>
    /// Every remaining case uses only the types we implement, being Integer,
    /// Decimal, String, Token, Byte Sequence, Boolean, Inner List and
    /// Parameters, so our parser must answer true and the whole parsed value
    /// is compared against the value the suite gives.
    /// </para>
    /// <para>
    /// The suite writes a Token as an object with '__type' of 'token' and a
    /// Byte Sequence as an object with '__type' of 'binary' whose value is
    /// base32, not base64, so both forms are decoded here before the
    /// comparison. Our model holds a byte sequence as a byte array, an
    /// integer as a long, a decimal as a decimal, a string as a string, a
    /// boolean as a bool and a token as an SfToken.
    /// </para>
    /// </remarks>
    [TestClass]
    public class StructuredFieldConformanceTests
    {
        /// <summary>
        /// Supplied by the test framework and used to print the count of
        /// cases in each bucket.
        /// </summary>
        public TestContext TestContext { get; set; }

        /// <summary>
        /// The folder, relative to the test assembly, holding the vendored
        /// suite.
        /// </summary>
        private const string SuiteFolder = "StructuredFieldTests";

        /// <summary>
        /// The files of the suite that bear on what we parse. The files
        /// covering bare lists, dates and display strings are deliberately
        /// not vendored. SOURCE.txt records that choice.
        /// </summary>
        private static readonly string[] SuiteFiles = new[]
        {
            "binary.json",
            "boolean.json",
            "dictionary.json",
            "examples.json",
            "item.json",
            "key-generated.json",
            "number.json",
            "param-dict.json",
            "string-generated.json",
            "string.json",
            "token-generated.json",
            "token.json",
        };

        /// <summary>
        /// Which rule a case is judged by. See the class remarks for what
        /// each one means.
        /// </summary>
        public enum CaseBucket
        {
            /// <summary>
            /// A bare list, which the element never parses, so the case is
            /// not run.
            /// </summary>
            NotRunBareList,

            /// <summary>
            /// The suite says every parser must reject the input.
            /// </summary>
            MustFail,

            /// <summary>
            /// The suite allows either outcome.
            /// </summary>
            CanFail,

            /// <summary>
            /// Valid input that needs a type we do not implement, so our
            /// parser is expected to reject it.
            /// </summary>
            UnsupportedType,

            /// <summary>
            /// Valid input using only types we implement, so our parser
            /// must accept it and read the value the suite gives.
            /// </summary>
            SupportedMustParse,
        }

        /// <summary>
        /// One case of the suite, already read from JSON and classified.
        /// </summary>
        public sealed class ConformanceCase
        {
            /// <summary>
            /// The suite file the case came from.
            /// </summary>
            public string File { get; set; }

            /// <summary>
            /// The name the suite gives the case.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// The suite header type, being 'item', 'list' or 'dictionary'.
            /// </summary>
            public string HeaderType { get; set; }

            /// <summary>
            /// The field value, being the lines of 'raw' joined with a comma
            /// and a space the way the suite requires.
            /// </summary>
            public string Raw { get; set; }

            /// <summary>
            /// The value the suite expects, converted into the shape
            /// described in the class remarks. Null when the case is a
            /// must fail case and gives no expected value.
            /// </summary>
            public object Expected { get; set; }

            /// <summary>
            /// The strict serialisation the suite expects, being the lines
            /// of 'canonical' joined the way <see cref="Raw"/> is joined.
            /// When the suite gives no 'canonical' the input is already in
            /// its strict form, so this is <see cref="Raw"/>.
            /// </summary>
            public string Canonical { get; set; }

            /// <summary>
            /// The rule this case is judged by.
            /// </summary>
            public CaseBucket Bucket { get; set; }

            /// <inheritdoc/>
            public override string ToString() =>
                File + " / " + Name;
        }

        /// <summary>
        /// A token as the suite writes it.
        /// </summary>
        private sealed class ExpectedToken
        {
            /// <summary>
            /// The characters of the token.
            /// </summary>
            public string Value { get; set; }
        }

        /// <summary>
        /// A date as the suite writes it. Our parser does not implement
        /// dates, so a case holding one should never reach the value
        /// comparison.
        /// </summary>
        private sealed class ExpectedDate
        {
            /// <summary>
            /// The seconds since the epoch.
            /// </summary>
            public long Value { get; set; }
        }

        /// <summary>
        /// A display string as the suite writes it. Our parser does not
        /// implement display strings, so a case holding one should never
        /// reach the value comparison.
        /// </summary>
        private sealed class ExpectedDisplayString
        {
            /// <summary>
            /// The characters of the display string.
            /// </summary>
            public string Value { get; set; }
        }

        /// <summary>
        /// An item as the suite writes it, being a bare value with its
        /// parameters. The bare value is a list of
        /// <see cref="ExpectedItem"/> when the item is an inner list.
        /// </summary>
        private sealed class ExpectedItem
        {
            /// <summary>
            /// The bare value.
            /// </summary>
            public object Value { get; set; }

            /// <summary>
            /// The parameters, in the order they were written.
            /// </summary>
            public IList<KeyValuePair<string, object>> Parameters
            {
                get;
                set;
            }
        }

        /// <summary>
        /// Every case of every vendored file, read once.
        /// </summary>
        private static readonly Lazy<IList<ConformanceCase>> AllCases =
            new Lazy<IList<ConformanceCase>>(LoadAllCases);

        /// <summary>
        /// The cases that are actually run, meaning everything except the
        /// bare list cases, one per test result.
        /// </summary>
        public static IEnumerable<object[]> Cases
        {
            get
            {
                foreach (var testCase in AllCases.Value)
                {
                    if (testCase.Bucket != CaseBucket.NotRunBareList)
                    {
                        yield return new object[] { testCase };
                    }
                }
            }
        }

        /// <summary>
        /// Name a test result after the file and case it came from, so that
        /// a failure says which case broke.
        /// </summary>
        /// <param name="methodInfo">The test method.</param>
        /// <param name="data">The arguments of the test method.</param>
        /// <returns>The display name.</returns>
        public static string CaseDisplayName(
            MethodInfo methodInfo,
            object[] data)
        {
            var testCase = data[0] as ConformanceCase;
            return testCase == null
                ? methodInfo.Name
                : testCase.File + " / " + testCase.Name;
        }

        /// <summary>
        /// Run one case of the suite through our parser and judge the
        /// outcome by the rule for the bucket the case is in.
        /// </summary>
        /// <param name="testCase">The case to run.</param>
        [DataTestMethod]
        [DynamicData(
            nameof(Cases),
            DynamicDataSourceType.Property,
            DynamicDataDisplayName = nameof(CaseDisplayName))]
        public void Conformance(ConformanceCase testCase)
        {
            var where =
                testCase.File + " / " + testCase.Name +
                ", raw '" + Describe(testCase.Raw) + "'";

            bool parsed;
            SfDictionary dictionary = null;
            SfItem item = null;
            if (string.Equals(
                testCase.HeaderType, "dictionary", StringComparison.Ordinal))
            {
                parsed = StructuredFieldParser.TryParseDictionary(
                    testCase.Raw, out dictionary);
            }
            else
            {
                parsed = StructuredFieldParser.TryParseItem(
                    testCase.Raw, out item);
            }

            switch (testCase.Bucket)
            {
                case CaseBucket.MustFail:
                    // The suite says no parser may accept this input, so
                    // accepting it is a defect in our parser.
                    Assert.IsFalse(
                        parsed,
                        "The suite requires this input to be rejected but " +
                        "the parser accepted it. " + where);
                    break;

                case CaseBucket.UnsupportedType:
                    // Valid RFC 9651 that needs a Date or a Display String.
                    // Our parser implements neither, so rejecting the input
                    // is the correct answer and is asserted rather than
                    // skipped so that a change of behaviour is noticed.
                    Assert.IsFalse(
                        parsed,
                        "This case needs a Date or a Display String, which " +
                        "the parser does not implement, so it was expected " +
                        "to be rejected. " + where);
                    break;

                case CaseBucket.CanFail:
                    // Either outcome is allowed, so only the value is
                    // checked and only when the parser accepted the input.
                    if (parsed)
                    {
                        CompareOutcome(testCase, dictionary, item, where);
                    }
                    break;

                default:
                    Assert.IsTrue(
                        parsed,
                        "The suite requires this input to be accepted but " +
                        "the parser rejected it. " + where);
                    CompareOutcome(testCase, dictionary, item, where);
                    break;
            }
        }

        /// <summary>
        /// Print how many cases fell into each bucket, so that a reviewer
        /// can see the suite is biting rather than being skipped wholesale.
        /// </summary>
        [TestMethod]
        public void SuiteCoverage()
        {
            var cases = AllCases.Value;
            var counts = new Dictionary<CaseBucket, int>();
            foreach (CaseBucket bucket in Enum.GetValues(typeof(CaseBucket)))
            {
                counts[bucket] = 0;
            }
            foreach (var testCase in cases)
            {
                counts[testCase.Bucket]++;
            }

            TestContext.WriteLine(
                "Structured field conformance suite, {0} cases read from " +
                "{1} files.",
                cases.Count,
                SuiteFiles.Length);
            TestContext.WriteLine(
                "  must fail, parser must reject:            {0}",
                counts[CaseBucket.MustFail]);
            TestContext.WriteLine(
                "  supported types, parser must accept:      {0}",
                counts[CaseBucket.SupportedMustParse]);
            TestContext.WriteLine(
                "  unsupported type, parser must reject:     {0}",
                counts[CaseBucket.UnsupportedType]);
            TestContext.WriteLine(
                "  can fail, either outcome allowed:         {0}",
                counts[CaseBucket.CanFail]);
            TestContext.WriteLine(
                "  bare list, not run:                       {0}",
                counts[CaseBucket.NotRunBareList]);

            Assert.IsTrue(
                counts[CaseBucket.MustFail] > 0,
                "No must fail cases were read, so the suite is not being " +
                "applied.");
            Assert.IsTrue(
                counts[CaseBucket.SupportedMustParse] > 0,
                "No supported cases were read, so the suite is not being " +
                "applied.");
        }

        /// <summary>
        /// Compare what the parser produced against what the suite says the
        /// value is.
        /// </summary>
        /// <param name="testCase">The case being run.</param>
        /// <param name="dictionary">
        /// The dictionary parsed, for a dictionary case.
        /// </param>
        /// <param name="item">The item parsed, for an item case.</param>
        /// <param name="where">Text naming the case in a failure.</param>
        private static void CompareOutcome(
            ConformanceCase testCase,
            SfDictionary dictionary,
            SfItem item,
            string where)
        {
            if (testCase.Expected == null)
            {
                Assert.Fail(
                    "The case gives no expected value to compare. " + where);
            }
            if (dictionary != null)
            {
                CompareDictionary(
                    (IList<KeyValuePair<string, ExpectedItem>>)
                        testCase.Expected,
                    dictionary,
                    where);
            }
            else
            {
                CompareItem(
                    (ExpectedItem)testCase.Expected, item, where, "item");
            }
        }

        /// <summary>
        /// Compare a parsed dictionary against the expected members, in
        /// order, because the signature base depends on the order.
        /// </summary>
        /// <param name="expected">The expected members.</param>
        /// <param name="actual">The dictionary parsed.</param>
        /// <param name="where">Text naming the case in a failure.</param>
        private static void CompareDictionary(
            IList<KeyValuePair<string, ExpectedItem>> expected,
            SfDictionary actual,
            string where)
        {
            Assert.AreEqual(
                expected.Count,
                actual.Members.Count,
                "The dictionary has the wrong number of members. " + where);
            for (var i = 0; i < expected.Count; i++)
            {
                var context = "member " + i;
                Assert.AreEqual(
                    expected[i].Key,
                    actual.Members[i].Key,
                    "The key of " + context + " is wrong. " + where);
                CompareMember(
                    expected[i].Value,
                    actual.Members[i].Value,
                    where,
                    context + " '" + expected[i].Key + "'");
            }
        }

        /// <summary>
        /// Compare one parsed dictionary member, which is either a single
        /// item or an inner list, against what the suite expects.
        /// </summary>
        /// <param name="expected">The expected member.</param>
        /// <param name="actual">The member parsed.</param>
        /// <param name="where">Text naming the case in a failure.</param>
        /// <param name="context">Text naming the member in a failure.</param>
        private static void CompareMember(
            ExpectedItem expected,
            SfMember actual,
            string where,
            string context)
        {
            var innerList = expected.Value as IList<ExpectedItem>;
            if (innerList == null)
            {
                Assert.IsFalse(
                    actual.IsInnerList,
                    "The " + context + " was read as an inner list but a " +
                    "single item was expected. " + where);
                CompareItem(expected, actual.Item, where, context);
                return;
            }

            Assert.IsTrue(
                actual.IsInnerList,
                "The " + context + " was read as a single item but an " +
                "inner list was expected. " + where);
            Assert.AreEqual(
                innerList.Count,
                actual.InnerList.Count,
                "The inner list of " + context + " has the wrong number of " +
                "items. " + where);
            for (var i = 0; i < innerList.Count; i++)
            {
                CompareItem(
                    innerList[i],
                    actual.InnerList[i],
                    where,
                    context + ", inner item " + i);
            }
            CompareParameters(
                expected.Parameters, actual.Parameters, where, context);
        }

        /// <summary>
        /// Compare one parsed item, being a bare value with its parameters,
        /// against what the suite expects.
        /// </summary>
        /// <param name="expected">The expected item.</param>
        /// <param name="actual">The item parsed.</param>
        /// <param name="where">Text naming the case in a failure.</param>
        /// <param name="context">Text naming the item in a failure.</param>
        private static void CompareItem(
            ExpectedItem expected,
            SfItem actual,
            string where,
            string context)
        {
            CompareValue(expected.Value, actual.Value, where, context);
            CompareParameters(
                expected.Parameters, actual.Parameters, where, context);
        }

        /// <summary>
        /// Compare the parameters of an item or an inner list, in order.
        /// </summary>
        /// <param name="expected">The expected parameters.</param>
        /// <param name="actual">The parameters parsed.</param>
        /// <param name="where">Text naming the case in a failure.</param>
        /// <param name="context">
        /// Text naming what the parameters belong to in a failure.
        /// </param>
        private static void CompareParameters(
            IList<KeyValuePair<string, object>> expected,
            IList<SfParameter> actual,
            string where,
            string context)
        {
            Assert.AreEqual(
                expected.Count,
                actual.Count,
                "The " + context + " has the wrong number of parameters. " +
                where);
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(
                    expected[i].Key,
                    actual[i].Name,
                    "The name of parameter " + i + " of " + context +
                    " is wrong. " + where);
                CompareValue(
                    expected[i].Value,
                    actual[i].Value,
                    where,
                    context + ", parameter '" + expected[i].Key + "'");
            }
        }

        /// <summary>
        /// Compare one bare value against what the suite expects, mapping
        /// the suite's forms onto the types our model uses.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The value parsed.</param>
        /// <param name="where">Text naming the case in a failure.</param>
        /// <param name="context">Text naming the value in a failure.</param>
        private static void CompareValue(
            object expected,
            object actual,
            string where,
            string context)
        {
            var prefix = "The value of " + context + " is wrong. ";
            if (expected is string text)
            {
                Assert.IsInstanceOfType(
                    actual, typeof(string), prefix + where);
                Assert.AreEqual(text, (string)actual, prefix + where);
            }
            else if (expected is bool flag)
            {
                Assert.IsInstanceOfType(
                    actual, typeof(bool), prefix + where);
                Assert.AreEqual(flag, (bool)actual, prefix + where);
            }
            else if (expected is long number)
            {
                Assert.IsInstanceOfType(
                    actual, typeof(long), prefix + where);
                Assert.AreEqual(number, (long)actual, prefix + where);
            }
            else if (expected is decimal fraction)
            {
                Assert.IsInstanceOfType(
                    actual, typeof(decimal), prefix + where);
                // Decimal equality here ignores trailing zeros, which is
                // what the suite wants because it treats 1.20 and 1.2 as
                // the same value.
                Assert.IsTrue(
                    fraction == (decimal)actual,
                    prefix + "Expected " + fraction + " but read " +
                    actual + ". " + where);
            }
            else if (expected is byte[] bytes)
            {
                Assert.IsInstanceOfType(
                    actual, typeof(byte[]), prefix + where);
                var read = (byte[])actual;
                Assert.IsTrue(
                    bytes.SequenceEqual(read),
                    prefix + "Expected " + Convert.ToBase64String(bytes) +
                    " but read " + Convert.ToBase64String(read) + ". " +
                    where);
            }
            else if (expected is ExpectedToken token)
            {
                Assert.IsInstanceOfType(
                    actual, typeof(SfToken), prefix + where);
                Assert.AreEqual(
                    token.Value, ((SfToken)actual).Value, prefix + where);
            }
            else
            {
                Assert.Fail(
                    "The case expects a value of a type the parser does " +
                    "not implement, so it should not have been compared. " +
                    where);
            }
        }

        /// <summary>
        /// Read every vendored file and classify every case in it.
        /// </summary>
        /// <returns>The cases, in file then file order.</returns>
        private static IList<ConformanceCase> LoadAllCases()
        {
            var folder = Path.Combine(
                Path.GetDirectoryName(
                    typeof(StructuredFieldConformanceTests).Assembly
                        .Location),
                "Fixtures",
                SuiteFolder);
            var cases = new List<ConformanceCase>();
            foreach (var file in SuiteFiles)
            {
                var path = Path.Combine(folder, file);
                if (File.Exists(path) == false)
                {
                    throw new FileNotFoundException(
                        "The vendored structured field test suite is " +
                        "missing a file. Check the copy rules in the test " +
                        "project file.",
                        path);
                }
                using (var document = JsonDocument.Parse(
                    File.ReadAllText(path)))
                {
                    foreach (var element in
                        document.RootElement.EnumerateArray())
                    {
                        cases.Add(ReadCase(file, element));
                    }
                }
            }
            return cases;
        }

        /// <summary>
        /// Read one case out of the JSON and work out which rule judges it.
        /// </summary>
        /// <param name="file">The file the case came from.</param>
        /// <param name="element">The JSON of the case.</param>
        /// <returns>The case.</returns>
        private static ConformanceCase ReadCase(
            string file,
            JsonElement element)
        {
            var lines = element.GetProperty("raw").EnumerateArray()
                .Select(line => line.GetString());
            var testCase = new ConformanceCase()
            {
                File = file,
                Name = element.GetProperty("name").GetString(),
                HeaderType =
                    element.GetProperty("header_type").GetString(),
                Raw = string.Join(", ", lines),
            };

            testCase.Canonical = element.TryGetProperty(
                "canonical", out var canonical)
                ? string.Join(
                    ", ",
                    canonical.EnumerateArray()
                        .Select(line => line.GetString()))
                : testCase.Raw;

            var mustFail =
                element.TryGetProperty("must_fail", out var mustFailValue) &&
                mustFailValue.GetBoolean();
            var canFail =
                element.TryGetProperty("can_fail", out var canFailValue) &&
                canFailValue.GetBoolean();

            // The expected value of a bare list case is never read, both
            // because the case is not run and because it is written in a
            // shape this test does not model.
            var isList = string.Equals(
                testCase.HeaderType, "list", StringComparison.Ordinal);
            if (isList == false &&
                element.TryGetProperty("expected", out var expected))
            {
                testCase.Expected = string.Equals(
                    testCase.HeaderType,
                    "dictionary",
                    StringComparison.Ordinal)
                    ? ReadExpectedDictionary(expected)
                    : (object)ReadExpectedItem(expected);
            }

            if (isList)
            {
                testCase.Bucket = CaseBucket.NotRunBareList;
            }
            else if (mustFail)
            {
                testCase.Bucket = CaseBucket.MustFail;
            }
            else if (canFail)
            {
                testCase.Bucket = CaseBucket.CanFail;
            }
            else if (UsesUnsupportedType(testCase.Raw))
            {
                testCase.Bucket = CaseBucket.UnsupportedType;
            }
            else
            {
                testCase.Bucket = CaseBucket.SupportedMustParse;
            }
            return testCase;
        }

        /// <summary>
        /// Say whether a valid field value needs a type the parser does not
        /// implement, meaning a Date or a Display String. Quoted strings are
        /// stepped over so that an '@' or a '%' inside a string is not taken
        /// for the start of one of those types.
        /// </summary>
        /// <param name="raw">The field value.</param>
        /// <returns>
        /// True if the value needs a type we do not implement.
        /// </returns>
        private static bool UsesUnsupportedType(string raw)
        {
            for (var i = 0; i < raw.Length; i++)
            {
                var current = raw[i];
                if (current == '"')
                {
                    i++;
                    while (i < raw.Length && raw[i] != '"')
                    {
                        if (raw[i] == '\\')
                        {
                            i++;
                        }
                        i++;
                    }
                    continue;
                }
                if (current == '@')
                {
                    // A Date is written as an '@' then the seconds since
                    // the epoch.
                    return true;
                }
                if (current == '%' &&
                    i + 1 < raw.Length &&
                    raw[i + 1] == '"')
                {
                    // A Display String is written as a '%' then a quoted
                    // string of percent encoded bytes.
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Read the expected value of a dictionary case, which the suite
        /// writes as an array of key and member pairs.
        /// </summary>
        /// <param name="element">The JSON of the expected value.</param>
        /// <returns>The expected members, in order.</returns>
        private static IList<KeyValuePair<string, ExpectedItem>>
            ReadExpectedDictionary(JsonElement element)
        {
            var members = new List<KeyValuePair<string, ExpectedItem>>();
            foreach (var entry in element.EnumerateArray())
            {
                members.Add(
                    new KeyValuePair<string, ExpectedItem>(
                        entry[0].GetString(),
                        ReadExpectedItem(entry[1])));
            }
            return members;
        }

        /// <summary>
        /// Read the expected value of an item, which the suite writes as an
        /// array holding the bare value and then the parameters.
        /// </summary>
        /// <param name="element">The JSON of the expected item.</param>
        /// <returns>The expected item.</returns>
        private static ExpectedItem ReadExpectedItem(JsonElement element)
        {
            return new ExpectedItem()
            {
                Value = ReadExpectedValue(element[0]),
                Parameters = ReadExpectedParameters(element[1]),
            };
        }

        /// <summary>
        /// Read the expected parameters, which the suite writes as an array
        /// of name and value pairs.
        /// </summary>
        /// <param name="element">The JSON of the parameters.</param>
        /// <returns>The expected parameters, in order.</returns>
        private static IList<KeyValuePair<string, object>>
            ReadExpectedParameters(JsonElement element)
        {
            var parameters = new List<KeyValuePair<string, object>>();
            foreach (var entry in element.EnumerateArray())
            {
                parameters.Add(
                    new KeyValuePair<string, object>(
                        entry[0].GetString(),
                        ReadExpectedValue(entry[1])));
            }
            return parameters;
        }

        /// <summary>
        /// Read one expected bare value, turning the suite's forms into the
        /// types our model uses. An array is an inner list of items.
        /// </summary>
        /// <param name="element">The JSON of the value.</param>
        /// <returns>The expected value.</returns>
        private static object ReadExpectedValue(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number:
                    return ReadExpectedNumber(element);
                case JsonValueKind.Array:
                    var items = new List<ExpectedItem>();
                    foreach (var entry in element.EnumerateArray())
                    {
                        items.Add(ReadExpectedItem(entry));
                    }
                    return items;
                case JsonValueKind.Object:
                    return ReadExpectedTyped(element);
                default:
                    throw new FormatException(
                        "The suite holds an expected value of a kind this " +
                        "test does not read, being " + element.ValueKind +
                        ".");
            }
        }

        /// <summary>
        /// Read a number, deciding from how the suite wrote it whether it
        /// is an Integer or a Decimal, because the two are different types
        /// in a structured field.
        /// </summary>
        /// <param name="element">The JSON of the number.</param>
        /// <returns>A long or a decimal.</returns>
        private static object ReadExpectedNumber(JsonElement element)
        {
            var text = element.GetRawText();
            if (text.IndexOf('.') >= 0 ||
                text.IndexOf('e') >= 0 ||
                text.IndexOf('E') >= 0)
            {
                return decimal.Parse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture);
            }
            return long.Parse(text, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Read one of the values the suite writes as an object carrying a
        /// '__type' name, being a token, a byte sequence, a date or a
        /// display string.
        /// </summary>
        /// <param name="element">The JSON of the value.</param>
        /// <returns>The expected value.</returns>
        private static object ReadExpectedTyped(JsonElement element)
        {
            var name = element.GetProperty("__type").GetString();
            var value = element.GetProperty("value");
            switch (name)
            {
                case "token":
                    return new ExpectedToken()
                    {
                        Value = value.GetString(),
                    };
                case "binary":
                    // The suite writes byte sequences in base32 even though
                    // a field carries them in base64.
                    return FromBase32(value.GetString());
                case "date":
                    return new ExpectedDate()
                    {
                        Value = value.GetInt64(),
                    };
                case "displaystring":
                    return new ExpectedDisplayString()
                    {
                        Value = value.GetString(),
                    };
                default:
                    throw new FormatException(
                        "The suite holds an expected value of a type this " +
                        "test does not read, being '" + name + "'.");
            }
        }

        /// <summary>
        /// Decode the base32 the suite uses for byte sequences, as
        /// RFC 4648 section 6 defines it.
        /// </summary>
        /// <param name="encoded">The base32 text.</param>
        /// <returns>The bytes.</returns>
        private static byte[] FromBase32(string encoded)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var bytes = new List<byte>();
            var buffer = 0;
            var bits = 0;
            foreach (var character in encoded)
            {
                if (character == '=')
                {
                    break;
                }
                var index = alphabet.IndexOf(character);
                if (index < 0)
                {
                    throw new FormatException(
                        "The suite holds a byte sequence that is not " +
                        "base32, being '" + encoded + "'.");
                }
                buffer = (buffer << 5) | index;
                bits += 5;
                if (bits >= 8)
                {
                    bits -= 8;
                    bytes.Add((byte)((buffer >> bits) & 0xFF));
                }
            }
            return bytes.ToArray();
        }

        /// <summary>
        /// Make a field value safe to print, because several cases hold
        /// control characters or characters outside ASCII that would
        /// otherwise garble the test output.
        /// </summary>
        /// <param name="value">The text to describe.</param>
        /// <returns>
        /// The text with awkward characters written as escapes.
        /// </returns>
        private static string Describe(string value)
        {
            var builder = new StringBuilder();
            foreach (var character in value)
            {
                if (character < ' ' || character > '~')
                {
                    builder.Append("\\u").Append(
                        ((int)character).ToString(
                            "x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append(character);
                }
            }
            return builder.ToString();
        }
    }
}
