/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
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

using System.IO;

namespace FiftyOne.Pipeline.Engines.Data.Readers
{
    /// <summary>
    /// Used to load data from either a file or byte array into the format 'T'.
    /// </summary>
    /// <typeparam name="T">Type of object to load</typeparam>
    public interface IDataLoader<T>
    {
        /// <summary>
        /// Load a new instance of T using the data in the file provided.
        /// </summary>
        /// <param name="filePath">File to load from</param>
        /// <returns></returns>
        T LoadData(string filePath);

        /// <summary>
        /// Load a new instance of T using the data stream provided.
        /// </summary>
        /// <param name="data">Data to load from</param>
        /// <returns></returns>
        T LoadData(Stream data);
    }
}
