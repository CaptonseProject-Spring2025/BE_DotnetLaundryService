using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaundryService.Infrastructure
{
    public static class HangfireConfig
    {
        public static IServiceCollection AddHangfireServices(this IServiceCollection services,
                                                         IConfiguration cfg)
        {
            services.AddHangfire(global =>
            {
                global.UseSimpleAssemblyNameTypeSerializer()
                      .UseRecommendedSerializerSettings()

                      // ⭐ overload mới
                      .UsePostgreSqlStorage(
                          options => options.UseNpgsqlConnection(cfg.GetConnectionString("DbConnection")),
                          new PostgreSqlStorageOptions
                          {
                              SchemaName = "hangfire",  // tuỳ chọn (null = public)
                              PrepareSchemaIfNecessary = true
                          });
            });

            services.AddHangfireServer(serverOptions =>
            {
                serverOptions.WorkerCount = Environment.ProcessorCount;
                serverOptions.ServerName = "Laundry-HF";
            });

            return services;
        }
    }
}
