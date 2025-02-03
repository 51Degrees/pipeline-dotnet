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

using FiftyOne.Pipeline.Math;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


/// @example NetCore/Startup.cs
/// 
/// ## ASP.NET core example
/// 
/// This example shows how to:
/// 
/// 1. Set up configuration options to add elements to the 51Degrees Pipeline.
/// @dontinclude NetCore/appsettings.json
/// @until //{
/// @skipline //}
/// @skipline ]
/// @line }
/// @line }
/// 
/// 2. Configure HostBuilder to use Startup class.
/// @dontinclude NetCore/Program.cs
/// @skip namespace
/// @until UseStartup
/// @line }
/// @line }
/// @line }
/// 
/// 3. Populate ViewData in HomeController.
/// @dontinclude NetCore/Controllers/HomeController.cs
/// @skip namespace
/// @until return
/// @line }
/// @line }
/// @line }
/// 
/// 4. Add the MathElemenetBuilder to the services collection so that
/// the Pipeline creation process knows where to find it.
/// ```{cs}
/// public class HomeController : Controller
/// {
///     ...
///     public void ConfigureServices(IServiceCollection services)
///     {
///         ...
///         services.AddSingleton<MathElementBuilder>();
///         ...
/// ```
/// 5. Call AddFiftyOne to add all the things the Pipeline will need
/// to the services collection and create it based on the supplied
/// configruation.
/// ```{cs}
///         ...
///         services.AddFiftyOne(Configuration);
///         ...
///     }
/// ...
/// ```
/// 
/// 6. Call UseFiftyOne to add the Middleware component that will send any
/// requests through the 51Degrees pipeline.
/// ```{cs}
/// ...
///     public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
///     {
///         ...
///         app.UseFiftyOne();
///         ...
///     }
/// ...
/// ```
/// 
/// ## Startup
/// 
namespace AspNetCore_Example
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();

            // Add the MathElemenetBuilder to the services collection so that
            // the Pipeline creation process knows where to find it.
            services.AddSingleton<MathElementBuilder>();

            // Call AddFiftyOne to add all the things the Pipeline will need
            // to the services collection and create it based on the supplied
            // configruation.
            services.AddFiftyOne(Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            // Call UseFiftyOne to add the Middleware component that will send any
            // requests through the 51Degrees pipeline. 
            app.UseFiftyOne();

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
