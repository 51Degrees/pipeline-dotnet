
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Translation.Data;
using FiftyOne.Pipeline.Translation.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System;

namespace FiftyOne.Pipeline.Translation.Tests;

[TestClass]
public class TranslationEngineTests
{
    private ILoggerFactory _loggerFactory = LoggerFactory.Create(b => { });

    /// <summary>
    /// Test that a string value is correctly translated based on the provided
    /// translation file and language evidence.
    /// </summary>
    [TestMethod]
    public void TranslateString()
    {
        var flowData = SetupFrenchAnimals();

        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.AddEvidence("Animal", "dog");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var translation = result["AnimalTranslated"];

        Assert.IsNotNull(translation);
        Assert.AreEqual("chien", translation);
    }

    /// <summary>
    /// Test that different formats of the accept-language header are correctly
    /// parsed and the language extracted from them.
    /// </summary>
    /// <param name="header"></param>
    [DataRow("fr-FR, fr;q=0.9, en;q=0.8, de;q=0.7, *;q=0.5")]
    [DataRow("fr,sr;q=0.9,uk;q=0.8,en-US;q=0.7,en;q=0.6,ru;q=0.5,hr;q=0.4")]
    [DataRow("fr")]
    [DataRow("fr-FR")]
    [DataRow("fr_FR")]
    [TestMethod]
    public void HeaderFormats(string header)
    {
        var flowData = SetupFrenchAnimals();

        flowData.AddEvidence("header.accept-language", header);
        flowData.AddEvidence("Animal", "dog");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var translation = result["AnimalTranslated"];

        Assert.IsNotNull(translation);
        Assert.AreEqual("chien", translation);
    }

    /// <summary>
    /// Test that a string value is treated case insensitively when translating.
    /// </summary>
    [TestMethod]
    public void TranslateDifferentCase()
    {
        var flowData = SetupFrenchAnimals();

        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.AddEvidence("Animal", "Dog");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var translation = result["AnimalTranslated"];

        Assert.IsNotNull(translation);
        Assert.AreEqual("chien", translation);
    }

    /// <summary>
    /// Test that an exception is thrown when building the engine if no
    /// sources are provided. It's important that this happens when building,
    /// way before processing happens.
    /// </summary>
    [TestMethod]
    public void NoSouces()
    {
        var builder = new TranslationEngineBuilder(_loggerFactory)
            .AddTranslation("Animal", "AnimalTranslated")
            .SetSourceElementDataKey("somekey");

        Assert.ThrowsException<ArgumentNullException>(builder.Build);
    }

    /// <summary>
    /// Test that an exception is thrown when building the engine if no
    /// translations are provided. It's important that this happens when building,
    /// way before processing happens.
    /// </summary>
    [TestMethod]
    public void NoTranslations()
    {
        using var file = CreateFile("animals.fr_FR.yml", new Dictionary<string, string>());
        var builder = new TranslationEngineBuilder(_loggerFactory)
            .AddSource(file.File.FullName)
            .SetSourceElementDataKey("somekey");

        Assert.ThrowsException<ArgumentNullException>(builder.Build);
    }

    /// <summary>
    /// Test that an exception is thrown when building the engine if no element
    /// source key is provided. It's important that this happens when building,
    /// way before processing happens.
    /// </summary>
    [TestMethod]
    public void NoSourceKey()
    {
        using var file = CreateFile("animals.fr_FR.yml", new Dictionary<string, string>());
        var builder = new TranslationEngineBuilder(_loggerFactory)
            .AddSource(file.File.FullName)
            .AddTranslation("Animal", "TranslatedAnimal");

        Assert.ThrowsException<ArgumentNullException>(builder.Build);
    }

    /// <summary>
    /// Test that the engine can be built from a string source instead of a file,
    /// and that it correctly translates values based on that source.
    /// This is important to test as the string source is designed to be used
    /// with embedded resources, which are not passed as files.
    /// </summary>
    [TestMethod]
    public void BuildFromString()
    {
        var source =
            "cat: chat\ndog: chien";

        var engine = new TranslationEngineBuilder(_loggerFactory)
            .SetSourceElementDataKey("evidencecopy")
            .AddSource("animals.fr_FR.yml", source)
            .AddTranslation("Animal", "AnimalTranslated")
            .Build();
        using var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(new EvidenceCopyElement(_loggerFactory.CreateLogger<EvidenceCopyElement>()))
            .AddFlowElement(engine)
            .SetSuppressProcessExceptions(true)
            .Build();

        using var flowData = pipeline.CreateFlowData();

        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.AddEvidence("Animal", "dog");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var translation = result["AnimalTranslated"];

        Assert.IsNotNull(translation);
        Assert.AreEqual("chien", translation);
    }

    /// <summary>
    /// Test that a list of strings is correctly translated.
    /// </summary>
    [TestMethod]
    public void TranslateList()
    {
        var flowData = SetupFrenchAnimals();

        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.AddEvidence("Animal", new List<string>() { "cat", "dog" });
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var translation = result["AnimalTranslated"];

        Assert.IsNotNull(translation);
        Assert.IsInstanceOfType(translation, typeof(IReadOnlyList<string>));
        var listValue = translation as IReadOnlyList<string>;
        Assert.AreEqual("chat", listValue[0]);
        Assert.AreEqual("chien", listValue[1]);
    }

    /// <summary>
    /// Test that an AspectPropertyValue string is correctly translated.
    /// </summary>
    [TestMethod]
    public void TranslateAspectString()
    {
        var flowData = SetupFrenchAnimals();

        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.AddEvidence("Animal", new AspectPropertyValue<string>("dog"));
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var translation = result["AnimalTranslated"];

        Assert.IsNotNull(translation);
        Assert.IsInstanceOfType(translation, typeof(IAspectPropertyValue<string>));
        var aspectValue = translation as IAspectPropertyValue<string>;

        Assert.IsTrue(aspectValue.HasValue);
        Assert.AreEqual("chien", aspectValue.Value);
    }

    /// <summary>
    /// Test that an AspectPropertyValue list of strings are correctly translated. 
    /// </summary>
    [TestMethod]
    public void TranslateAspectStringList()
    {
        var flowData = SetupFrenchAnimals();
        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.AddEvidence("Animal", new AspectPropertyValue<IReadOnlyList<string>>(["cat", "dog"]));
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var translation = result["AnimalTranslated"];

        Assert.IsNotNull(translation);
        Assert.IsInstanceOfType(translation, typeof(IAspectPropertyValue<IReadOnlyList<string>>));
        var aspectValue = translation as IAspectPropertyValue<IReadOnlyList<string>>;

        Assert.IsTrue(aspectValue.HasValue);
        Assert.AreEqual("chat", aspectValue.Value[0]);
        Assert.AreEqual("chien", aspectValue.Value[1]);
    }

    /// <summary>
    /// Test tahat a weighted list of strings are correctly translated, and their
    /// weightings maintained.
    /// </summary>
    [TestMethod]
    public void TranslateWeighted()
    {
        var flowData = SetupFrenchAnimals();
        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.AddEvidence("Animal",
            new AspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>([
                new WeightedValue<string>(1, "cat"),
                new WeightedValue<string>(2, "dog")]));
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var translation = result["AnimalTranslated"];

        Assert.IsNotNull(translation);
        Assert.IsInstanceOfType(translation,
            typeof(IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>));
        var aspectValue = translation as
            IAspectPropertyValue<IReadOnlyList<IWeightedValue<string>>>;

        Assert.IsTrue(aspectValue.HasValue);
        Assert.AreEqual("chat", aspectValue.Value[0].Value);
        Assert.AreEqual(1, aspectValue.Value[0].RawWeighting);
        Assert.AreEqual("chien", aspectValue.Value[1].Value);
        Assert.AreEqual(2, aspectValue.Value[1].RawWeighting);
    }

    /// <summary>
    /// Test that the translation engine behaves as expected when the language
    /// provided in the evidence is missing from the translation files. Each
    /// behavior is tested here.
    /// </summary>
    /// <param name="behavior"></param>
    [TestMethod]
    [DataRow(MissingTranslationBehavior.Original)]
    [DataRow(MissingTranslationBehavior.EmptyString)]
    [DataRow(MissingTranslationBehavior.FlowError)]
    public void MissingLanguage(MissingTranslationBehavior behavior)
    {
        var flowData = SetupFrenchAnimals(behavior: behavior);
        flowData.AddEvidence("header.accept-language", "de_DE");
        flowData.AddEvidence("Animal", "cat");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();

        var translation = result["AnimalTranslated"];

        if (behavior == MissingTranslationBehavior.Original)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, "cat");
        }
        else if (behavior == MissingTranslationBehavior.EmptyString)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, string.Empty);
        }
        else if (behavior == MissingTranslationBehavior.FlowError)
        {
            Assert.AreEqual(1, flowData.Errors.Count);
            Assert.IsInstanceOfType(flowData.Errors[0].ExceptionData, typeof(KeyNotFoundException));
            Assert.IsTrue(flowData.Errors[0].ExceptionData.Message.Contains("no translator configured"));
        }
    }

    /// <summary>
    /// Test that the builder throws an exception when an invalid language code
    /// is provided as a source file.
    /// </summary>
    [TestMethod]
    public void InvalidConfiguredLanguage()
    {
        using var file = CreateFile(
            "animals.mmm_kp.yml", new Dictionary<string, string>());

        Assert.ThrowsException<InvalidDataException>(() =>
            new TranslationEngineBuilder(_loggerFactory)
                .SetSourceElementDataKey("evidencecopy")
                .AddSource(file.File.FullName)
                .AddTranslation("Animal", "AnimalTranslated")
                .Build());
    }

    /// <summary>
    /// Test that the translation engine behaves as expected when the language
    /// provided in the evidence is in an invalid format. Each behavior is
    /// tested here.
    /// </summary>
    /// <param name="behavior"></param>
    [TestMethod]
    [DataRow(MissingTranslationBehavior.Original)]
    [DataRow(MissingTranslationBehavior.EmptyString)]
    [DataRow(MissingTranslationBehavior.FlowError)]
    public void InvalidLanguage(MissingTranslationBehavior behavior)
    {
        var flowData = SetupFrenchAnimals(behavior: behavior);
        flowData.AddEvidence("header.accept-language", "mm_llk");
        flowData.AddEvidence("Animal", "cat");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();

        var translation = result["AnimalTranslated"];

        if (behavior == MissingTranslationBehavior.Original)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, "cat");
        }
        else if (behavior == MissingTranslationBehavior.EmptyString)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, string.Empty);
        }
        else if (behavior == MissingTranslationBehavior.FlowError)
        {
            Assert.AreEqual(1, flowData.Errors.Count);
            Assert.IsInstanceOfType(flowData.Errors[0].ExceptionData, typeof(KeyNotFoundException));
        }
    }

    /// <summary>
    /// Test that the translation engine behaves as expected when there is no
    /// translation for the word provided in the evidence. Each behavior is
    /// tested here.
    /// </summary>
    /// <param name="behavior"></param>
    [TestMethod]
    [DataRow(MissingTranslationBehavior.Original)]
    [DataRow(MissingTranslationBehavior.EmptyString)]
    [DataRow(MissingTranslationBehavior.FlowError)]
    public void NoTranslation(MissingTranslationBehavior behavior)
    {
        var flowData = SetupFrenchAnimals(behavior: behavior);
        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.AddEvidence("Animal", "buffalo");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();

        var translation = result["AnimalTranslated"];

        if (behavior == MissingTranslationBehavior.Original)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, "buffalo");
        }
        else if (behavior == MissingTranslationBehavior.EmptyString)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, string.Empty);
        }
        else if (behavior == MissingTranslationBehavior.FlowError)
        {
            Assert.AreEqual(1, flowData.Errors.Count);
            Assert.IsInstanceOfType(flowData.Errors[0].ExceptionData, typeof(KeyNotFoundException));
            Assert.IsTrue(flowData.Errors[0].ExceptionData.Message.Contains("no translation found"));
        }
    }

    /// <summary>
    /// Test that an empty string value in the source files is treated the same
    /// as no translation being present at all. This makes it easier to see in
    /// the source files which words have not been translated yet.
    /// </summary>
    /// <param name="behavior"></param>
    [TestMethod]
    [DataRow(MissingTranslationBehavior.Original)]
    [DataRow(MissingTranslationBehavior.EmptyString)]
    [DataRow(MissingTranslationBehavior.FlowError)]
    public void EmptyTranslation(MissingTranslationBehavior behavior)
    {
        var flowData = SetupFrenchAnimals(
            behavior: behavior,
            additionalAnimals: new Dictionary<string, string>()
                {
                    { "buffalo", "" }
                });
        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.AddEvidence("Animal", "buffalo");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();

        var translation = result["AnimalTranslated"];

        if (behavior == MissingTranslationBehavior.Original)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, "buffalo");
        }
        else if (behavior == MissingTranslationBehavior.EmptyString)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, string.Empty);
        }
        else if (behavior == MissingTranslationBehavior.FlowError)
        {
            Assert.AreEqual(1, flowData.Errors.Count);
            Assert.IsInstanceOfType(flowData.Errors[0].ExceptionData, typeof(KeyNotFoundException));
            Assert.IsTrue(flowData.Errors[0].ExceptionData.Message.Contains("no translation"));
        }
    }

    /// <summary>
    /// Test that using a wildcard format for the source files correctly adds
    /// 2 source files matching the wildcard.
    /// </summary>
    [TestMethod]
    public void WildCard()
    {
        var french = new Dictionary<string, string>()
        {
            { "cat", "chat" },
            { "dog", "chien" }
        };
        var german = new Dictionary<string, string>()
        {
            { "cat", "katze" },
            { "dog", "hund" }
        };

        using var frenchFile = CreateFile("animals.fr_FR.yml", french);
        using var germanFile = CreateFile("animals.de_DE.yml", french);

        var wildcard = frenchFile.File.Directory.FullName +
            Path.DirectorySeparatorChar +
            "animals.*.yml";
        var engine = new TranslationEngineBuilder(_loggerFactory)
            .SetSourceElementDataKey("evidencecopy")
            .AddSource(wildcard)
            .AddTranslation("Animal", "AnimalTranslated")
            .Build();
       
        Assert.IsTrue(engine.Languages.TryGetTranslator("fr_FR", out var frenchTranslator));
        Assert.IsTrue(engine.Languages.TryGetTranslator("de_DE", out var germanTranslator));
    }

    /// <summary>
    /// Test that when an AspectPropertyValue with no value is provided, the
    /// translation engine passes on the same no value reason.
    /// </summary>
    [TestMethod]
    public void NoAspectValue()
    {
        var flowData = SetupFrenchAnimals();

        var expected = new AspectPropertyValue<string>();
        expected.NoValueMessage = "Test no value message";
        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.AddEvidence("Animal", expected);
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var translation = result["AnimalTranslated"];

        Assert.IsNotNull(translation);
        Assert.IsInstanceOfType(translation, typeof(IAspectPropertyValue<string>));
        var aspectValue = translation as IAspectPropertyValue<string>;
        Assert.IsFalse(aspectValue.HasValue);
        Assert.AreEqual(expected.NoValueMessage, aspectValue.NoValueMessage);
    }

    /// <summary>
    /// Test that the translation engine correctly translates values when a
    /// fixed language is set. This test does not add a target language to the
    /// evidence, so will not work unless the language is taken from the fixed
    /// option.
    /// </summary>
    [TestMethod]
    public void FixedLanguage()
    {
        var expected = new Dictionary<string, string>()
        {
            { "cat", "chat" },
            { "dog", "chien" }
        };
        using var file = CreateFile("animals.fr_FR.yml", expected);

        var engine = new TranslationEngineBuilder(_loggerFactory)
            .SetSourceElementDataKey("evidencecopy")
            .AddSource(file.File.FullName)
            .SetFixedLanguage("fr_FR")
            .AddTranslation("Animal", "AnimalTranslated")
            .Build();
        using var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(new EvidenceCopyElement(_loggerFactory.CreateLogger<EvidenceCopyElement>()))
            .AddFlowElement(engine)
            .SetSuppressProcessExceptions(true)
            .Build();

        using var flowData = pipeline.CreateFlowData();

        flowData.AddEvidence("Animal", "cat");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var translation = result["AnimalTranslated"];

        Assert.IsNotNull(translation);
        Assert.IsInstanceOfType(translation, typeof(string));
        var value = translation as string;
        Assert.AreEqual("chat", value);
    }

    /// <summary>
    /// Test that 2 translations can be chained. This test uses an example of
    /// translating a country code to the English country name, then to the
    /// target language country name.
    /// </summary>
    [TestMethod]
    public void TestChained()
    {
        var codes = new Dictionary<string, string>()
        {
            { "GB", "United Kingdom" }
        };
        var countries = new Dictionary<string, string>()
        {
            { "United Kingdom", "Royaume-Uni" }
        };
        using var codesFile = CreateFile("codes.en_GB.yml", codes);
        using var countriesFile = CreateFile("countries.fr_FR.yml", countries);

        var codesEngine = new TranslationEngineBuilder(_loggerFactory)
            .SetSourceElementDataKey("evidencecopy")
            .AddSource(codesFile.File.FullName)
            .SetFixedLanguage("en_GB")
            .AddTranslation("CountryCode", "Country")
            .Build();
        var countriesEngine = new TranslationEngineBuilder(_loggerFactory)
            .SetSourceElementDataKey(codesEngine.ElementDataKey)
            .AddSource(countriesFile.File.FullName)
            .AddTranslation("Country", "CountryTranslated")
            .Build();
        using var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(new EvidenceCopyElement(_loggerFactory.CreateLogger<EvidenceCopyElement>()))
            .AddFlowElement(codesEngine)
            .AddFlowElement(countriesEngine)
            .SetSuppressProcessExceptions(true)
            .Build();

        using var flowData = pipeline.CreateFlowData();

        flowData.AddEvidence("CountryCode", "GB");
        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var country = result["Country"];
        var countryTranslated = result["CountryTranslated"];

        Assert.IsNotNull(country);
        Assert.IsNotNull(countryTranslated);
        Assert.IsInstanceOfType(country, typeof(string));
        Assert.IsInstanceOfType(countryTranslated, typeof(string));
        Assert.AreEqual("United Kingdom", country as string);
        Assert.AreEqual("Royaume-Uni", countryTranslated as string);
        Assert.AreEqual(
            1,
            flowData.ElementDataAsEnumerable()
                .Where(i => i is ITranslationData)
                .Count());
        Assert.IsTrue(flowData.Errors == null || flowData.Errors.Count == 0);
    }

    /// <summary>
    /// Test that when English is the preferred language in Accept-Language,
    /// the base translation engine passes through values unchanged (no
    /// translation), even when other languages with translators are present
    /// at lower priority.
    /// </summary>
    [TestMethod]
    public void EnglishPreferred_NoTranslation()
    {
        var flowData = SetupFrenchAnimals();

        flowData.AddEvidence(
            "header.accept-language",
            "en-US,en;q=0.9,fr;q=0.5");
        flowData.AddEvidence("Animal", "dog");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();
        var translation = result["AnimalTranslated"];

        // Should be "dog" (English pass-through), NOT "chien" (French).
        Assert.IsNotNull(translation);
        Assert.AreEqual("dog", translation);
    }

    /// <summary>
    /// Test that TryResolveLocale returns false when English is the
    /// preferred language, preventing fallthrough to lower-priority
    /// languages.
    /// </summary>
    [TestMethod]
    public void TryResolveLocale_EnglishPreferred_ReturnsFalse()
    {
        var locales = new[] { "fr_FR", "de_DE" };
        var result = Languages.TryResolveLocale(
            "en-US,en;q=0.9,fr;q=0.5",
            locales,
            out var matched);

        Assert.IsFalse(result);
        Assert.IsNull(matched);
    }

    /// <summary>
    /// Test that TryResolveLocale still finds non-English languages
    /// when they are preferred over English.
    /// </summary>
    [TestMethod]
    public void TryResolveLocale_FrenchPreferredOverEnglish()
    {
        var locales = new[] { "fr_FR", "de_DE" };
        var result = Languages.TryResolveLocale(
            "fr,en;q=0.5",
            locales,
            out var matched);

        Assert.IsTrue(result);
        Assert.AreEqual("fr_FR", matched);
    }

    /// <summary>
    /// Test that TryGetTranslator with locale output returns the matched
    /// locale key for an exact locale match.
    /// </summary>
    [TestMethod]
    public void TryGetTranslator_ReturnsMatchedLocale_ExactMatch()
    {
        var french = new Dictionary<string, string>()
        {
            { "cat", "chat" }
        };
        using var file = CreateFile("animals.fr_FR.yml", french);

        var engine = new TranslationEngineBuilder(_loggerFactory)
            .SetSourceElementDataKey("evidencecopy")
            .AddSource(file.File.FullName)
            .AddTranslation("Animal", "AnimalTranslated")
            .Build();

        var found = engine.Languages.TryGetTranslator(
            "fr_FR", out var translator, out var matchedLocale);

        Assert.IsTrue(found);
        Assert.IsNotNull(translator);
        Assert.AreEqual("fr_FR", matchedLocale);
    }

    /// <summary>
    /// Test that TryGetTranslator with locale output returns the matched
    /// locale key when only a 2-char language code is provided.
    /// </summary>
    [TestMethod]
    public void TryGetTranslator_ReturnsMatchedLocale_TwoCharCode()
    {
        var french = new Dictionary<string, string>()
        {
            { "cat", "chat" }
        };
        using var file = CreateFile("animals.fr_FR.yml", french);

        var engine = new TranslationEngineBuilder(_loggerFactory)
            .SetSourceElementDataKey("evidencecopy")
            .AddSource(file.File.FullName)
            .AddTranslation("Animal", "AnimalTranslated")
            .Build();

        var found = engine.Languages.TryGetTranslator(
            "fr", out var translator, out var matchedLocale);

        Assert.IsTrue(found);
        Assert.IsNotNull(translator);
        Assert.AreEqual("fr_FR", matchedLocale);
    }

    /// <summary>
    /// Test that TryGetTranslator picks the highest-priority language
    /// from an Accept-Language header and returns its locale.
    /// </summary>
    [TestMethod]
    public void TryGetTranslator_ReturnsMatchedLocale_AcceptLanguageHeader()
    {
        var french = new Dictionary<string, string>()
        {
            { "cat", "chat" }
        };
        var german = new Dictionary<string, string>()
        {
            { "cat", "katze" }
        };
        using var frFile = CreateFile("animals.fr_FR.yml", french);
        using var deFile = CreateFile("animals.de_DE.yml", german);

        var engine = new TranslationEngineBuilder(_loggerFactory)
            .SetSourceElementDataKey("evidencecopy")
            .AddSource(frFile.File.Directory.FullName +
                Path.DirectorySeparatorChar + "animals.*.yml")
            .AddTranslation("Animal", "AnimalTranslated")
            .Build();

        // French is preferred (higher quality), should match fr_FR.
        var found = engine.Languages.TryGetTranslator(
            "fr,de-DE;q=0.5",
            out var translator,
            out var matchedLocale);

        Assert.IsTrue(found);
        Assert.IsNotNull(translator);
        Assert.AreEqual("fr_FR", matchedLocale);
    }

    /// <summary>
    /// Test that TryGetTranslator respects preference order and picks the
    /// preferred language even when a lower-priority language has an exact
    /// locale match in the header.
    /// </summary>
    [TestMethod]
    public void TryGetTranslator_PrefersHigherPriority_OverExactLocale()
    {
        var french = new Dictionary<string, string>()
        {
            { "cat", "chat" }
        };
        var german = new Dictionary<string, string>()
        {
            { "cat", "katze" }
        };
        using var frFile = CreateFile("animals.fr_FR.yml", french);
        using var deFile = CreateFile("animals.de_DE.yml", german);

        var engine = new TranslationEngineBuilder(_loggerFactory)
            .SetSourceElementDataKey("evidencecopy")
            .AddSource(frFile.File.Directory.FullName +
                Path.DirectorySeparatorChar + "animals.*.yml")
            .AddTranslation("Animal", "AnimalTranslated")
            .Build();

        // "fr" (2-char, no locale) is preferred over "de-DE" (exact locale).
        var found = engine.Languages.TryGetTranslator(
            "fr,de-DE;q=0.5",
            out var translator,
            out var matchedLocale);

        Assert.IsTrue(found);
        Assert.AreEqual("fr_FR", matchedLocale);
    }

    /// <summary>
    /// Test that TryGetTranslator returns false and null locale when no
    /// matching language is found.
    /// </summary>
    [TestMethod]
    public void TryGetTranslator_NoMatch_ReturnsNullLocale()
    {
        var french = new Dictionary<string, string>()
        {
            { "cat", "chat" }
        };
        using var file = CreateFile("animals.fr_FR.yml", french);

        var engine = new TranslationEngineBuilder(_loggerFactory)
            .SetSourceElementDataKey("evidencecopy")
            .AddSource(file.File.FullName)
            .AddTranslation("Animal", "AnimalTranslated")
            .Build();

        var found = engine.Languages.TryGetTranslator(
            "ja_JP", out var translator, out var matchedLocale);

        Assert.IsFalse(found);
        Assert.IsNull(translator);
        Assert.IsNull(matchedLocale);
    }

    /// <summary>
    /// Test that the original TryGetTranslator overload (without locale)
    /// still works correctly after the refactoring.
    /// </summary>
    [TestMethod]
    public void TryGetTranslator_OriginalOverload_StillWorks()
    {
        var french = new Dictionary<string, string>()
        {
            { "cat", "chat" }
        };
        using var file = CreateFile("animals.fr_FR.yml", french);

        var engine = new TranslationEngineBuilder(_loggerFactory)
            .SetSourceElementDataKey("evidencecopy")
            .AddSource(file.File.FullName)
            .AddTranslation("Animal", "AnimalTranslated")
            .Build();

        var found = engine.Languages.TryGetTranslator(
            "fr_FR", out var translator);

        Assert.IsTrue(found);
        Assert.IsNotNull(translator);
    }

    /// <summary>
    /// Sets up a basic test pipeline with french translations for "cat" and
    /// "dog".
    /// </summary>
    /// <param name="behavior"></param>
    /// <param name="additionalAnimals"></param>
    /// <returns></returns>
    private IFlowData SetupFrenchAnimals(
        MissingTranslationBehavior behavior = MissingTranslationBehavior.Original,
        Dictionary<string, string> additionalAnimals = null)
    {
        var expected = new Dictionary<string, string>()
        {
            { "cat", "chat" },
            { "dog", "chien" }
        };
        if (additionalAnimals != null)
        {
            foreach (var addition in additionalAnimals)
            {
                expected.Add(addition.Key, addition.Value);
            }
        }
        using var file = CreateFile("animals.fr_FR.yml", expected);

        var engine = new TranslationEngineBuilder(_loggerFactory)
            .SetSourceElementDataKey("evidencecopy")
            .SetMissingTranslationBehavior(behavior)
            .AddSource(file.File.FullName)
            .AddTranslation("Animal", "AnimalTranslated")
            .Build();
        var pipeline = new PipelineBuilder(_loggerFactory)
            .AddFlowElement(new EvidenceCopyElement(_loggerFactory.CreateLogger<EvidenceCopyElement>()))
            .AddFlowElement(engine)
            .SetSuppressProcessExceptions(true)
            .Build();

        var flowData = pipeline.CreateFlowData();

        return flowData;
    }

    /// <summary>
    /// Basic class used to ensure that a file is deleted at the end of tests.
    /// </summary>
    private class DisposableFile : IDisposable
    {
        public readonly FileInfo File;

        public DisposableFile(FileInfo file)
        {
            File = file;
        }

        public void Dispose()
        {
            if (File.Exists)
            {
                File.Delete();
            }
        }
    }

    /// <summary>
    /// Creates a YAML file with the content required. Uses the
    /// <see cref="DisposableFile"/> class, so this can be called in a "using"
    /// to ensure the file is deleted after the test.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="content"></param>
    /// <returns></returns>
    private static DisposableFile CreateFile(
        string name, Dictionary<string, string> content)
    {
        var file = new FileInfo(Path.Combine(Path.GetTempPath(), name));
        using (var writer = new StreamWriter(file.FullName))
        {
            foreach (var kvp in content)
            {
                writer.WriteLine($"{kvp.Key}: {kvp.Value}");
            }
        }

        return new DisposableFile(file);
    }
}