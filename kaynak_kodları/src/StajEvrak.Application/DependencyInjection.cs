using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StajEvrak.Application.Applications;
using StajEvrak.Application.Auth;
using StajEvrak.Application.Comparison;
using StajEvrak.Application.Dataset;
using StajEvrak.Application.Documents;
using StajEvrak.Application.Files;
using StajEvrak.Application.Profiles;
using StajEvrak.Application.Users;

namespace StajEvrak.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<UploadOptions>(configuration.GetSection(UploadOptions.SectionName));

        services.AddSingleton<IPasswordService, PasswordService>();

        // Kabul edilen evrak türleri: her imza sınıfı bir dosya türünü temsil eder.
        services.AddSingleton<FileSignature, DocxSignature>();
        services.AddSingleton<FileSignature, DocSignature>();
        services.AddSingleton<FileSignature, PdfSignature>();
        services.AddSingleton<FileSignature, JpegSignature>();
        services.AddSingleton<FileSignature, PngSignature>();
        services.AddSingleton<IUploadFileValidator, UploadFileValidator>();

        services.AddSingleton<ComparisonService>();
        services.AddScoped<AuthService>();
        services.AddScoped<DatasetImportService>();
        services.AddScoped<DocumentUploadService>();
        services.AddScoped<StudentProfileService>();
        services.AddScoped<ApplicationService>();
        services.AddScoped<UserAdministrationService>();
        return services;
    }
}
