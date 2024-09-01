/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2023 51 Degrees Mobile Experts Limited, Davidson House,
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

using Newtonsoft.Json;
using System.IO;

namespace FiftyOne.Pipeline.Engines.Data.Readers
{
    /// <summary>
    /// JSON implementation of IDataLoader. This deserializes the data from a
    /// JSON file to the type T.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class JsonLoader<T> : IDataLoader<T>
    {
        /// <summary>
        /// Load data from a StreamReader.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns>A new instance of T</returns>
        private T LoadData(StreamReader reader)
        {
            return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
        }

        /// <summary>
        /// Load data from a file.
        /// </summary>
        /// <param name="filePath">
        /// The complete path to the file to load data from.
        /// </param>
        /// <returns>
        /// The data in the file as a new instance of type 'T'.
        /// </returns>
        public T LoadData(string filePath)
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                return LoadData(reader);
            }
        }

        /// <summary>
        /// Load data from a stream.
        /// </summary>
        /// <param name="data">
        /// The stream to load data from.
        /// </param>
        /// <returns>
        /// The data in the stream as a new instance of type 'T'.
        /// </returns>
        public T LoadData(Stream data)
        {
            using (StreamReader reader = new StreamReader(data))
            {
                return LoadData(reader);
            }
        }
    }
}
