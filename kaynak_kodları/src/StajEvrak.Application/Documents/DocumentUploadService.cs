using StajEvrak.Application.Abstractions;
using StajEvrak.Application.Common;
using StajEvrak.Application.Files;
using StajEvrak.Domain.Applications;
using StajEvrak.Domain.Common;
using StajEvrak.Domain.Users;

namespace StajEvrak.Application.Documents;

public sealed record StoredFile(string FullPath, string DownloadName, string ContentType);

/// <summary>
/// Evrak yükleme akışı: dosya kontrolü → saklama → başvuruya versiyon olarak ekleme → otomatik okuma → İnceleniyor.
/// </summary>
public sealed class DocumentUploadService(
    IUploadFileValidator validator,
    IFileStorage storage,
    IDocumentReaderFactory readerFactory,
    IUserRepository users,
    IApplicationRepository applications,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public IReadOnlyCollection<string> AllowedExtensions => validator.AllowedExtensions;

    public async Task<OperationResult<UploadedDocument>> UploadAsync(int studentId, string fileName, long length, Stream content, CancellationToken ct = default)
    {
        var student = await users.GetStudentAsync(studentId, ct);
        if (student is null) return OperationResult<UploadedDocument>.Fail("Öğrenci bulunamadı.");

        // Docx içerik kontrolü için akışın ileri-geri okunabilmesi gerekir.
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var validation = validator.Validate(fileName, length, buffer);
        if (!validation.IsValid) return OperationResult<UploadedDocument>.Fail(validation.Error!);

        var application = await applications.GetByStudentAsync(studentId, ct);
        if (application is null)
        {
            application = new InternshipApplication(studentId, clock.UtcNow);
            applications.Add(application);
        }

        if (!application.CanBeModifiedBy(student))
            return OperationResult<UploadedDocument>.Fail(
                $"Başvurunuz '{application.State.DisplayName}' statüsünde olduğu için yeni belge yüklenemez.");

        buffer.Position = 0;
        var storedPath = await storage.SaveAsync(buffer, validation.Extension, $"student-{studentId}", ct);

        try
        {
            var safeName = Path.GetFileName(fileName);
            var document = application.AddDocument(student, safeName, storedPath, validation.Extension, length, clock.UtcNow);

            var reader = readerFactory.GetReader(validation.Extension);
            var read = reader.Read(storage.GetFullPath(storedPath));
            document.SetExtraction(read.Supported, read.Fields, read.Note);

            application.StartReview(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(ct);
            return OperationResult<UploadedDocument>.Ok(document);
        }
        catch (DomainException ex)
        {
            storage.Delete(storedPath);
            return OperationResult<UploadedDocument>.Fail(ex.Message);
        }
    }

    public async Task<OperationResult> DeleteDocumentAsync(int userId, int applicationId, int documentId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId, ct);
        var application = await applications.GetAsync(applicationId, ct);
        if (user is null || application is null || !user.CanAccess(application))
            return OperationResult.Fail("Başvuru bulunamadı.");

        var document = application.Documents.FirstOrDefault(d => d.Id == documentId);
        if (document is null) return OperationResult.Fail("Belge bulunamadı.");

        try
        {
            application.RemoveDocument(user, documentId, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(ct);
            storage.Delete(document.StoredPath);
            return OperationResult.Ok();
        }
        catch (DomainException ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public async Task<StoredFile?> GetFileAsync(int userId, int documentId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId, ct);
        if (user is null) return null;

        var application = await applications.GetByDocumentAsync(documentId, ct);
        if (application is null || !user.CanAccess(application)) return null;

        var document = application.Documents.FirstOrDefault(d => d.Id == documentId);
        if (document is null) return null;

        var contentType = document.Extension switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
        return new StoredFile(storage.GetFullPath(document.StoredPath), document.OriginalFileName, contentType);
    }
}
