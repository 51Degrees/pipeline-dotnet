using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Core.TypedMap;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Engines.Tests.FlowElements
{
    public class Helpers
    {
        /// <summary>
        /// Create a Source Element Data that the engine can read values from.
        /// </summary>
        /// <param name="flowData"></param>
        /// <param name="sourceData"></param>
        internal static void ConfigureSourceData(
            Mock<IFlowData> flowData,
            IElementData sourceData)
        {
            IElementData sourceDataOut = sourceData;
            flowData.Setup(d => d.TryGetValue<IElementData>(
                    It.IsAny<ITypedKey<IElementData>>(),
                    out sourceDataOut))
                .Returns(true);
        }

        /// <summary>
        /// Creates a temp directory. 
        /// </summary>
        /// <returns></returns>
        internal static string CreateTempDirectory(List<string> tempPaths)
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "translation-engine-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            tempPaths.Add(path);
            return path;
        }

        /// <summary>
        /// Creates a translation file. 
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="fileName"></param>
        /// <param name="translations"></param>
        /// <returns></returns>
        internal static FileInfo CreateTranslationFile(
            string directory,
            string fileName,
            IDictionary<string, string> translations)
        {
            var path = Path.Combine(directory, fileName);
            var lines = translations.Select(kvp => $"{kvp.Key}: {kvp.Value}");
            File.WriteAllLines(path, lines);
            return new FileInfo(path);
        }
    }
}
