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

using FiftyOne.Pipeline.AgentSignature.FlowElement;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;

namespace FiftyOne.Pipeline.AgentSignature.Tests.Standard
{
    /// <summary>
    /// Proves that this test project runs the netstandard2.0 build of the
    /// element, which is the build a .NET Framework consumer loads. Every
    /// other test in this project rests on these two checks, because a
    /// project reference that quietly fell back to the net8.0 build would
    /// leave all of them checking the System.Text.Json reading paths that
    /// the main test project already covers.
    /// </summary>
    [TestClass]
    public class AssemblyResolutionTests
    {
        /// <summary>
        /// The element assembly under test was built for .NET Standard,
        /// not for net8.0.
        /// </summary>
        [TestMethod]
        public void TheElementAssemblyTargetsNetStandard()
        {
            var assembly = typeof(AgentSignatureElement).Assembly;
            var framework = assembly
                .GetCustomAttribute<TargetFrameworkAttribute>();
            Assert.IsNotNull(
                framework,
                "Expected the element assembly to state the framework it " +
                "was built for.");
            StringAssert.Contains(
                framework.FrameworkName,
                ".NETStandard",
                "Expected the netstandard2.0 build of the element, and " +
                "the loaded assembly was built for '" +
                framework.FrameworkName + "'. The project reference is " +
                "not resolving the target framework it names.");
        }

        /// <summary>
        /// The element assembly under test references Newtonsoft.Json,
        /// which only its netstandard2.0 build does. This pins the build
        /// to the one carrying the Newtonsoft branch of JsonReader.
        /// </summary>
        [TestMethod]
        public void TheElementAssemblyReadsJsonWithNewtonsoft()
        {
            var references = typeof(AgentSignatureElement).Assembly
                .GetReferencedAssemblies()
                .Select(r => r.Name)
                .ToList();
            Assert.IsTrue(
                references.Contains("Newtonsoft.Json"),
                "Expected the element assembly to reference " +
                "Newtonsoft.Json, because the netstandard2.0 build reads " +
                "JSON with that library. The assemblies referenced were: " +
                string.Join(", ", references) + ".");
        }
    }
}
