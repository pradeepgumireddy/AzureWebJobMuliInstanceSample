using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PocWebJob.ConfigSettings;

namespace PocWebJob
{
    class Program
    {
        public static void Main(string[] args)
        {
            ServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            var builder = new HostBuilder();
            builder.ConfigureWebJobs(b =>
            {
                b.AddAzureStorageCoreServices();
                b.AddTimers();
            });
            builder.ConfigureServices(s =>
            {
                s.AddTransient<Functions, Functions>();
                s.Configure<ServiceBusSettings>(Configuration.GetSection("ServiceBusSettings"));
                s.Configure<CommonSettings>(Configuration.GetSection("CommonSettings"));
            });
            builder.ConfigureLogging((context, b) =>
            {
                b.AddConsole();
                //If the key exists in settings, use it to enable Application Insights.
                string instrumentationKey = context.Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];
                if (!string.IsNullOrEmpty(instrumentationKey))
                {
                    b.AddApplicationInsights(o => o.InstrumentationKey = instrumentationKey);
                }
            });
            var host = builder.Build();
            using (host)
            {
                host.Run();
            }
        }
        private static IConfiguration Configuration { get; set; }

        private static void ConfigureServices(IServiceCollection services)
        {
            //var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                //.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            services.AddSingleton(Configuration);
        }
    }
}
