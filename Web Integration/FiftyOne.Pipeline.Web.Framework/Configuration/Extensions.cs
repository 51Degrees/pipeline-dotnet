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

using FiftyOne.Pipeline.Core.Exceptions;
using FiftyOne.Pipeline.Web.Shared;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using System;
using System.IO;
using System.Web;

namespace FiftyOne.Pipeline.Web.Framework.Configuration
{
    /// <summary>
    /// Extension methods used by the Web.Framework project.
    /// </summary>
#pragma warning disable CA1724 // Type names should not match namespaces
    public static class Extensions
#pragma warning restore CA1724 // Type names should not match namespaces
    {

        private const string JSON_SCHEMA_DIR = "Schemas";
        private const string JSON_SCHEMA_FILENAME = "pipelineOptionsSchema.json";

        /// <summary>
        /// Function used to determine the base directory. Defaults to using
        /// the HttpContext. Provided to enable tests operating without a web
        /// server to be performed.
        /// HttpContext.Current.Server.MapPath("~/App_Data")
        /// </summary>
        public static Func<string> BaseDirectory = 
            () => HttpContext.Current.Server.MapPath("~/App_Data");

        /// <summary>
        /// Find and add a configuration file in the web application's
        /// "App_Data" folder to the server configuration builder.
        /// </summary>
        /// <param name="config">
        /// Configuration builder to add the config file to
        /// </param>
        /// <returns>
        /// The same configuration builder
        /// </returns>
        /// <exception cref="PipelineConfigurationException">
        /// Thrown if the configuration file is not valid.
        /// </exception>
        public static IConfigurationBuilder AddPipelineConfig(this IConfigurationBuilder config)
        {
            var basePath = BaseDirectory();
            // Try possible XML files.
            foreach (var fileName in Constants.ConfigFileNames)
            {
                foreach (var extension in Constants.XmlFileExtensions)
                {
                    if (File.Exists(Path.Combine(basePath, fileName + "." + extension)))
                    {
                        // Note - Schema validation not applied to XML.
                        return config.SetBasePath(basePath)
                            .AddXmlFile(fileName + "." + extension);
                    }
                }
            }
            // Try possible JSON files.
            foreach (var fileName in Constants.ConfigFileNames)
            {
                foreach (var extension in Constants.JsonFileExtensions)
                {
                    var filePath = Path.Combine(basePath, fileName + "." + extension);
                    if (File.Exists(filePath))
                    {
                        ValidateJson(filePath);

                        return config.SetBasePath(basePath)
                            .AddJsonFile(fileName + "." + extension);
                    }
                }
            }
            return config;
        }

        /// <summary>
        /// Returns the full path to the json settings file.
        /// </summary>
        private static string JsonSchemaFullPath =>
            Path.Combine(BaseDirectory(), JSON_SCHEMA_DIR, JSON_SCHEMA_FILENAME);

        /// <summary>
        /// Validate the specified json file using the pipeline options schema.
        /// </summary>
        /// <param name="jsonFile">
        /// The file to validate.
        /// </param>
        /// <exception cref="PipelineConfigurationException">
        /// Thrown if the json file is not valid.
        /// </exception>
        private static void ValidateJson(string jsonFile)
        {            
            // In some cases, the schema file is not going to be where we expect it to be.
            // We don't want to crash the user's system and we don't have a logger in here,
            // so just handle it by skipping validation.
            if (File.Exists(JsonSchemaFullPath))
            {
                JSchema schema = JSchema.Parse(File.ReadAllText(JsonSchemaFullPath));

                // Prepare the reader
                JsonTextReader reader = new JsonTextReader(new StringReader(File.ReadAllText(jsonFile)));

                // Add the schema validation
                JSchemaValidatingReader validatingReader = new JSchemaValidatingReader(reader);
                validatingReader.Schema = schema;

                JsonSerializer serializer = new JsonSerializer();
                // Try reading the content. Catch validation exceptions and rethrow with a 
                // more friendly error message.
                try
                {
                    dynamic temp = serializer.Deserialize<dynamic>(validatingReader);
                }
                catch (JSchemaValidationException ex)
                {
                    throw new PipelineConfigurationException(
                        string.Format(Messages.ExceptionInvalidConfiguration, jsonFile), ex);
                }
            }
        }
    }
}
