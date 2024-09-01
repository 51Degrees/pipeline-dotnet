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

using FiftyOne.Pipeline.Web;
using FiftyOne.Pipeline.Core.FlowElements;
using FiftyOne.Pipeline.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;

/// <summary>
/// Class that holds the static extension methods that are added to various
/// MVC classes.
/// </summary>
public static class FiftyOneExtensions
{
    /// <summary>
    /// Directs the MVC ApplicationBuilder to use the 51Degrees middleware
    /// component to provide Pipeline functionality.
    /// </summary>
    /// <remarks>
    /// Note: if any of the ExceptionHandlerExtensions.UseExceptionHandler
    /// methods are used, they MUST be called before this method in order
    /// to correctly handle exception.
    /// </remarks>
    /// <param name="builder">The <see cref="IApplicationBuilder"/></param>
    /// <returns>The <see cref="IApplicationBuilder"/></returns>
    public static IApplicationBuilder UseFiftyOne(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<FiftyOneMiddleware>();
    }

    /// <summary>
    /// Configures the specified service collection with the services 
    /// required by <see cref="IPipeline"/>.
    /// </summary>
    /// <typeparam name="TBuilder">
    /// The type of pipeline builder to use when building the pipeline.
    /// </typeparam>
    /// <param name="services">
    /// The DI container
    /// </param>
    /// <param name="configuration">
    /// The configuration object
    /// </param>
    /// <param name="pipelineFactory">
    /// The function to use when building the pipeline. By default, 
    /// this will use the configuration from <see cref="PipelineOptions"/> 
    /// </param>
    public static void AddFiftyOne<TBuilder>(this IServiceCollection services,
        IConfiguration configuration,
        Func<IConfiguration, IPipelineBuilderFromConfiguration, IPipeline> pipelineFactory = null)
        where TBuilder : class, IPipelineBuilderFromConfiguration
    {
        FiftyOneStartup.ConfigureServices<TBuilder>(services, configuration, pipelineFactory);
    }


    /// <summary>
    /// Configures the specified service collection with the services 
    /// required by <see cref="IPipeline"/>.
    /// The <see cref="FiftyOnePipelineBuilder"/> is used to build 
    /// the pipeline. To change this, use the generic override of this method.
    /// </summary>
    /// <param name="services">
    /// The DI container
    /// </param>
    /// <param name="configuration">
    /// The configuration object
    /// </param>
    /// <param name="pipelineFactory">
    /// The function to use when building the pipeline. By default, 
    /// this will use the configuration from <see cref="PipelineOptions"/> 
    /// </param>
    public static void AddFiftyOne(this IServiceCollection services,
        IConfiguration configuration,
        Func<IConfiguration, IPipelineBuilderFromConfiguration, IPipeline> pipelineFactory = null)
    {
        AddFiftyOne<FiftyOnePipelineBuilder>(services, configuration, pipelineFactory);
    }
}
