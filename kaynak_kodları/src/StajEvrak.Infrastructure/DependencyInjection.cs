using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StajEvrak.Application.Abstractions;
using StajEvrak.Application.Dataset;
using StajEvrak.Application.Documents;
using StajEvrak.Infrastructure.Data;
using StajEvrak.Infrastructure.Files;
using StajEvrak.Infrastructure.Importers;
using StajEvrak.Infrastructure.Readers;

namespace StajEvrak.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string contentRoot)
    {
        var connection = configuration.GetConnectionString("Default") ?? "Data Source=App_Data/stajevrak.db";
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connection);
        if (!Path.IsPathRooted(builder.DataSource))
            builder.DataSource = Path.Combine(contentRoot, builder.DataSource);
        Directory.CreateDirectory(Path.GetDirectoryName(builder.DataSource)!);

        services.AddDbContext<AppDbContext>(o => o.UseSqlite(builder.ToString()));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ILoginAttemptRepository, LoginAttemptRepository>();
        services.AddScoped<IDatasetRepository, DatasetRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();

        services.AddSingleton<IClock, SystemClock>();

        var uploads = configuration["Storage:UploadsPath"] ?? "App_Data/uploads";
        if (!Path.IsPathRooted(uploads)) uploads = Path.Combine(contentRoot, uploads);
        services.AddSingleton<IFileStorage>(new LocalFileStorage(uploads));

        services.AddSingleton<DatasetParser, ExcelDatasetParser>();
        services.AddSingleton<DatasetParser, CsvDatasetParser>();

        services.AddSingleton<IDocumentReader, DocxEk16Reader>();
        services.AddSingleton<IDocumentReader, PdfEk16Reader>();
        services.AddSingleton<IDocumentReader, ImageDocumentReader>();
        services.AddSingleton<IDocumentReader, LegacyDocReader>();
        services.AddSingleton<IDocumentReaderFactory, DocumentReaderFactory>();

        services.AddScoped<DbSeeder>();
        return services;
    }
}
