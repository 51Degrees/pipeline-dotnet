
using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Elements.Translation.Data;
using FiftyOne.Pipeline.Elements.Translation.FlowElements;
using FiftyOne.Pipeline.Engines.Data;
using FiftyOne.Pipeline.Translation.Data;
using Microsoft.Extensions.Logging;

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
    /// behaviour is tested here.
    /// </summary>
    /// <param name="behaviour"></param>
    [TestMethod]
    [DataRow(MissingTranslationBehaviour.Original)]
    [DataRow(MissingTranslationBehaviour.EmptyString)]
    [DataRow(MissingTranslationBehaviour.FlowError)]
    public void MissingLanguage(MissingTranslationBehaviour behaviour)
    {
        var flowData = SetupFrenchAnimals(behaviour: behaviour);
        flowData.AddEvidence("header.accept-language", "de_DE");
        flowData.AddEvidence("Animal", "cat");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();

        var translation = result["AnimalTranslated"];

        if (behaviour == MissingTranslationBehaviour.Original)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, "cat");
        }
        else if (behaviour == MissingTranslationBehaviour.EmptyString)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, string.Empty);
        }
        else if (behaviour == MissingTranslationBehaviour.FlowError)
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
    /// provided in the evidence is in an invalid format. Each behaviour is
    /// tested here.
    /// </summary>
    /// <param name="behaviour"></param>
    [TestMethod]
    [DataRow(MissingTranslationBehaviour.Original)]
    [DataRow(MissingTranslationBehaviour.EmptyString)]
    [DataRow(MissingTranslationBehaviour.FlowError)]
    public void InvalidLanguage(MissingTranslationBehaviour behaviour)
    {
        var flowData = SetupFrenchAnimals(behaviour: behaviour);
        flowData.AddEvidence("header.accept-language", "mm_llk");
        flowData.AddEvidence("Animal", "cat");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();

        var translation = result["AnimalTranslated"];

        if (behaviour == MissingTranslationBehaviour.Original)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, "cat");
        }
        else if (behaviour == MissingTranslationBehaviour.EmptyString)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, string.Empty);
        }
        else if (behaviour == MissingTranslationBehaviour.FlowError)
        {
            Assert.AreEqual(1, flowData.Errors.Count);
            Assert.IsInstanceOfType(flowData.Errors[0].ExceptionData, typeof(KeyNotFoundException));
            Assert.IsTrue(flowData.Errors[0].ExceptionData.Message.Contains("did not contain a language"));
        }
    }

    /// <summary>
    /// Test that the translation engine behaves as expected when there is no
    /// translation for the word provided in the evidence. Each behaviour is
    /// tested here.
    /// </summary>
    /// <param name="behaviour"></param>
    [TestMethod]
    [DataRow(MissingTranslationBehaviour.Original)]
    [DataRow(MissingTranslationBehaviour.EmptyString)]
    [DataRow(MissingTranslationBehaviour.FlowError)]
    public void NoTranslation(MissingTranslationBehaviour behaviour)
    {
        var flowData = SetupFrenchAnimals(behaviour: behaviour);
        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.AddEvidence("Animal", "buffalo");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();

        var translation = result["AnimalTranslated"];

        if (behaviour == MissingTranslationBehaviour.Original)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, "buffalo");
        }
        else if (behaviour == MissingTranslationBehaviour.EmptyString)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, string.Empty);
        }
        else if (behaviour == MissingTranslationBehaviour.FlowError)
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
    /// <param name="behaviour"></param>
    [TestMethod]
    [DataRow(MissingTranslationBehaviour.Original)]
    [DataRow(MissingTranslationBehaviour.EmptyString)]
    [DataRow(MissingTranslationBehaviour.FlowError)]
    public void EmptyTranslation(MissingTranslationBehaviour behaviour)
    {
        var flowData = SetupFrenchAnimals(
            behaviour: behaviour,
            additionalAnimals: new Dictionary<string, string>()
                {
                    { "buffalo", "" }
                });
        flowData.AddEvidence("header.accept-language", "fr_FR");
        flowData.AddEvidence("Animal", "buffalo");
        flowData.Process();

        var result = flowData.Get<ITranslationData>();

        var translation = result["AnimalTranslated"];

        if (behaviour == MissingTranslationBehaviour.Original)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, "buffalo");
        }
        else if (behaviour == MissingTranslationBehaviour.EmptyString)
        {
            Assert.IsNotNull(translation);
            Assert.AreEqual(translation, string.Empty);
        }
        else if (behaviour == MissingTranslationBehaviour.FlowError)
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
    /// Sets up a basic test pipeline with french translations for "cat" and 
    /// "dog".
    /// </summary>
    /// <param name="behaviour"></param>
    /// <param name="additionalAnimals"></param>
    /// <returns></returns>
    private IFlowData SetupFrenchAnimals(
        MissingTranslationBehaviour behaviour = MissingTranslationBehaviour.Original,
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
            .SetMissingTranslationBehaviour(behaviour)
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