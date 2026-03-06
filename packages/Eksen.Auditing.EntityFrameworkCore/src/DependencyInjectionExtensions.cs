using Eksen.Auditing.EntityFrameworkCore;
using Eksen.Auditing.Repositories;
using Eksen.EntityFrameworkCore;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IEksenAuditingBuilder builder)
    {
        public IEksenAuditingBuilder UseEntityFrameworkCore<TDbContext>()
            where TDbContext : EksenDbContext
        {
            var services = builder.Services;

            services.AddScoped<IAuditLogRepository, EfCoreAuditLogRepository<TDbContext>>();
            services.AddScoped<IAuditLogActionRepository, EfCoreAuditLogActionRepository<TDbContext>>();
            services.AddScoped<IAuditLogEntityChangeRepository, EfCoreAuditLogEntityChangeRepository<TDbContext>>();
            services.AddScoped<IAuditLogPropertyChangeRepository, EfCoreAuditLogPropertyChangeRepository<TDbContext>>();

            return builder;
        }
    }
}
