using Es.Riam.AbstractsOpen;
using Es.Riam.Gnoss.AD.EntityModel;
using Es.Riam.Gnoss.AD.EntityModelBASE;
using Es.Riam.Gnoss.AD.Virtuoso;
using Es.Riam.Gnoss.CL;
using Es.Riam.Gnoss.Servicios;
using Es.Riam.Gnoss.Util.Configuracion;
using Es.Riam.Gnoss.Util.General;
using Es.Riam.Gnoss.Util.Seguridad;
using Es.Riam.OpenReplication;
using Es.Riam.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gnoss.BackgroundTask.ThumbnailGenerator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService() //Windows
                .UseSystemd() //Linux
                .ConfigureServices((hostContext, services) =>
                {
                    IConfiguration configuration = hostContext.Configuration;
                    services.AddScoped(typeof(UtilTelemetry));
                    services.AddScoped(typeof(Usuario));
                    services.AddScoped(typeof(UtilPeticion));

                    services.AddScoped(typeof(RedisCacheWrapper));
                    services.AddScoped(typeof(UtilidadesVirtuoso));
                    services.AddScoped(typeof(VirtuosoAD));
                    services.AddScoped(typeof(LoggingService));
                    services.AddScoped(typeof(GnossCache));
                    services.AddSingleton<ConfigService>();
                    services.AddSingleton<ILoggerFactory, LoggerFactory>();
                    services.AddScoped<IServicesUtilVirtuosoAndReplication, ServicesVirtuosoAndBidirectionalReplicationOpen>();
                    IDictionary environmentVariables = Environment.GetEnvironmentVariables();

                    string acid = "";
                    if (environmentVariables.Contains("acid"))
                    {
                        acid = environmentVariables["acid"] as string;
                    }
                    else
                    {
                        acid = configuration.GetConnectionString("acid");
                    }
                    string baseConnection = "";
                    if (environmentVariables.Contains("base"))
                    {
                        baseConnection = environmentVariables["base"] as string;
                    }
                    else
                    {
                        baseConnection = configuration.GetConnectionString("base");
                    }
                    string bdType = "";
                    if (environmentVariables.Contains("connectionType"))
                    {
                        bdType = environmentVariables["connectionType"] as string;
                    }
                    else
                    {
                        bdType = configuration.GetConnectionString("connectionType");
                    }
                    if (bdType.Equals("0"))
                    {
                        services.AddDbContext<EntityContext>(options =>
                                options.UseSqlServer(acid)
                                );
                        services.AddDbContext<EntityContextBASE>(options =>
                                options.UseSqlServer(baseConnection)

                                );
                    }
                    else if (bdType.Equals("2"))
                    {
                        services.AddEntityFrameworkNpgsql().AddDbContext<EntityContext>(opt =>
                        {
                            var builder = new NpgsqlDbContextOptionsBuilder(opt);
                            builder.SetPostgresVersion(new Version(9, 6));
                            opt.UseNpgsql(acid);

                        });
                        services.AddEntityFrameworkNpgsql().AddDbContext<EntityContextBASE>(opt =>
                        {
                            var builder = new NpgsqlDbContextOptionsBuilder(opt);
                            builder.SetPostgresVersion(new Version(9, 6));
                            opt.UseNpgsql(baseConnection);

                        });
                    }
                    services.AddHostedService<ThumbnailGeneratorWorker>();
                });
    }
}