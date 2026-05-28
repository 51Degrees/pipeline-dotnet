using FiftyOne.Did.Cloud.FlowElements;
using FiftyOne.Did.Core.Data;
using FiftyOne.Did.OnPremise.FlowElements;
using FiftyOne.IpIntelligence.Examples.OnPremise.GettingStartedAPI;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FiftyOne.Did.Core.FlowElements;
using FiftyOne.Did.OnPremise.Pooling;
using FiftyOne.Did.TestHelpers;
using FiftyOne.Pipeline.Configuration.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.LicenseElement.FlowElement;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FiftyOne.Did.OnPremise.Tests;

using static FODidValidationHelper;

[TestClass]
public class DidCloudEngineProcessingTests
{
    private static WebApplication? _cloudApp;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        Assembly.Load(typeof(ValidateLicenseElementBuilder).Assembly.GetName());
        Assembly.Load(typeof(DidOnPremiseEngineBuilder).Assembly.GetName());
    }

    private string _baseUrl = string.Empty;

    [TestInitialize]
    public async Task TestInitialize()
    {
        _cloudApp = new Program
        {
            UnconditionalEvidenceInjector = evidence =>
            {
                // required by ConfigurationElementBuilder
                evidence["cloud.api-version"] = "4";
                evidence[Pipeline.Entitlement.Common.Constants.EVIDENCE_LICENSE_KEY]
                    = TestEvidenceSets.CloudMasterKey.Key;
            },
            ServiceInjector = services =>
            {
                services.AddPooled<ResettableSHA256>();
                services.AddSingleton<ConfigurationElementBuilder>();
                services.AddSingleton<ValidateLicenseElementBuilder>();
                services.AddSingleton<DidOnPremiseEngineBuilder>();
            },
        }.BuildWebApp();
        await _cloudApp.StartAsync();
        _baseUrl = _cloudApp.Urls.First().Replace("0.0.0.0", "localhost");
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        if (_cloudApp is null)
        {
            return;
        }
        var source = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _cloudApp.StopAsync(source.Token);
        await _cloudApp.DisposeAsync();
        _cloudApp = null;
    }
        
    [TestMethod]
    public async Task DoHttpRequest()
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri(_baseUrl);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await client.GetAsync("/accessibleproperties");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        JArray? didProps;
        {
            Assert.AreNotEqual(string.Empty, json,
                "Accessible properties should respond with JSON");
            var propsResponse = JObject.Parse(json);
            var productsToken = GetIgnoreCase(propsResponse, "products");
            Assert.IsNotNull(productsToken,
                "Accessible properties response should contain products");
            var products = productsToken as JObject;
            Assert.IsNotNull(products, "Products should be a dictionary");
            var key = DidBaseEnginePropertiesBuilder.ComponentName;
            var didToken = GetIgnoreCase(products, key);
            Assert.IsNotNull(productsToken,
                $"Products should have Did data under '{key}'");
            var didData = didToken as JObject;
            Assert.IsNotNull(didData, "Did data should be an object");
            var didPropsToken = GetIgnoreCase(didData, "properties");
            Assert.IsNotNull(didPropsToken, "Did data should have properties");
            didProps = didPropsToken as JArray;
            Assert.IsNotNull(didProps, "Did properties should be an array");
            Assert.IsTrue(didProps.Count > 0, "Did properties should not be empty");
        }
        for (int i = 0; i < didProps.Count; i++)
        {
            var nextProp = didProps[i] as JObject;
            Assert.IsNotNull(nextProp, 
                $"Property at offset {i} should be an object");
            string propName;
            {
                var nameToken = GetIgnoreCase(nextProp, "name");
                Assert.IsNotNull(nameToken,
                    $"Property at offset {i} should have a name");
                Assert.AreEqual(JTokenType.String, nameToken.Type,
                    $"The name of property at offset {i} should be a string");
                propName = nameToken.ToString();
                Assert.IsFalse(string.IsNullOrWhiteSpace(propName),
                    $"The name of property at offset {i} shouldn't be a whitespace");
            }
            {
                var typeToken = GetIgnoreCase(nextProp, "type");
                Assert.IsNotNull(typeToken,
                    $"Property at offset {i} should have a type");
                Assert.AreEqual(JTokenType.String, typeToken.Type,
                    $"The type of property '{propName}' (at offset {i}) should be a string");
                Assert.IsFalse(string.IsNullOrWhiteSpace(typeToken.ToString()),
                    $"The type of property '{propName}' (at offset {i}) shouldn't be a whitespace");
                Assert.DoesNotContain("`1", typeToken.ToString(),
                    $"The type of property '{propName}' (at offset {i}) shouldn't contain (`1)");
            }
        }
    }
    
    private static JToken? GetIgnoreCase(JObject obj, string key)
    {
        // Fast path: exact match
        if (obj.TryGetValue(key, out var value))
            return value;

        // Slow path: case-insensitive search
        foreach (var prop in obj.Properties())
        {
            if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
                return prop.Value;
        }

        return null;
    }
        
    [TestMethod]
    [DataRow(null, false)]
    [DataRow("standard", true)]
    [DataRow("crabby", false)]
    public async Task DoFullRequest(string? usageString, bool isValidUsage)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("testsettings.json")
            .Build();

        // Bind the configuration to a pipeline options instance
        var options = new PipelineOptions();
        var section = config.GetRequiredSection("PipelineOptions");
        
        // Use the 'ErrorOnUnknownConfiguration' option to warn us if we've got any
        // misnamed configuration keys.
        section.Bind(options, (o) =>
        {
            o.ErrorOnUnknownConfiguration = true;
        });
        
        var requestParams = options.Elements.First(x =>
            x.BuilderName == nameof(CloudRequestEngine));
        requestParams.BuildParameters["EndPoint"] = _baseUrl;

        // Initialize a service collection which will be used to create the services
        // required by the Pipeline and manage their lifetimes.
        using var serviceProvider = new ServiceCollection()
            // Add the configuration to the services collection.
            .AddSingleton(options)
            // Make sure we're logging to the console.
            .AddLogging(l => l.AddConsole())
            // Add an HttpClient instance. This is used for making requests to the
            // Cloud service.
            .AddSingleton<HttpClient>()
            // Add the builders that will be needed to create the engines specified in the 
            // configuration file.
            .AddSingleton<CloudRequestEngineBuilder>()
            .AddSingleton<DidCloudEngineBuilder>()
            .BuildServiceProvider();
        
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        using var pipeline = new FiftyOnePipelineBuilder(loggerFactory, serviceProvider)
            .BuildFromConfiguration(options);
        using var flowData = pipeline.CreateFlowData();
        flowData.AddEvidence(new Dictionary<string, object>
        {
            { "server.client-ip", "127.0.0.1" },
            { "header.sec-ch-ua-platform", "\"Android\"" },
            { "header.sec-ch-ua-mobile", "?1" },
            { "header.user-agent", "Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Mobile Safari/537.36" },
            { "header.sec-ch-ua", "\"Not(A:Brand\";v=\"8\", \"Chromium\";v=\"144\", \"Google Chrome\";v=\"144\"" },
        });
        if (usageString is not null)
        {
            flowData.AddEvidence("query.id.usage", usageString);
        }

        flowData.Process();

        var outData = flowData.Get<I51DidData>();
        if (isValidUsage == false)
        {
            bool shouldBeCompletelyNull = usageString is null;
            ValidateNoId(outData.IdProbGlobal, shouldBeCompletelyNull);
            ValidateNoId(outData.IdProbLic, shouldBeCompletelyNull);
            return;
        }

        var usageFlagsVal = DidOnPremiseEngine.UsageFlags[usageString]; 
        var expectedContents = new FoDidContents(
            UsageFlags: usageFlagsVal.Value,
            LicenseKeyId: usageFlagsVal.RequiresLicense
                ? TestEvidenceSets.CloudMasterKey.ID
                : 19493
        );
        await ValidateId(outData.IdProbGlobal, expectedContents);
        await ValidateId(outData.IdProbLic, expectedContents);
    }
}
