using StajEvrak.Domain.Applications;
using StajEvrak.Domain.Common;
using StajEvrak.Domain.Dataset;
using StajEvrak.Domain.Profiles;

namespace StajEvrak.Domain.Users;

public class Student : User
{
    private Student() { }

    public Student(string userName, string email, string studentNo, string fullName, DateTime createdAt)
        : base(userName, email, createdAt)
    {
        if (string.IsNullOrWhiteSpace(studentNo)) throw new DomainException("Öğrenci numarası boş olamaz.");
        StudentNo = studentNo.Trim();
        FullName = fullName?.Trim() ?? string.Empty;
    }

    public string StudentNo { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;

    public int? DatasetStudentId { get; private set; }
    public DatasetStudent? DatasetStudent { get; private set; }

    public StudentProfile? Profile { get; private set; }

    public override string Role => Roles.Student;
    public override string HomePath => "/Student";

    // Öğrenci yalnızca kendi başvurusuna erişebilir.
    public override bool CanAccess(InternshipApplication application) => application.StudentId == Id;

    // Öğrenci hesabını yalnızca başvuruları yöneten (admin) kullanıcı silebilir; kendi hesabını silemez.
    public override bool CanBeDeletedBy(User actor) => actor.CanReviewApplications && actor.Id != Id;

    public void LinkToDataset(DatasetStudent datasetStudent)
    {
        DatasetStudent = datasetStudent;
        DatasetStudentId = datasetStudent.Id;
    }

    public void UnlinkDataset()
    {
        DatasetStudent = null;
        DatasetStudentId = null;
    }

    public void ChangeStudentNo(string studentNo)
    {
        if (string.IsNullOrWhiteSpace(studentNo)) throw new DomainException("Öğrenci numarası boş olamaz.");
        StudentNo = studentNo.Trim();
    }

    public void ChangeFullName(string? fullName) => FullName = fullName?.Trim() ?? string.Empty;
}
