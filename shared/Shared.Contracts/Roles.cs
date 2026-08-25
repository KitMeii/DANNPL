namespace Shared.Contracts;

/// <summary>Canonical role names used as JWT role-claim values across every service.</summary>
public static class Roles
{
    public const string Student = "Student";
    public const string Teacher = "Teacher";
    public const string Admin = "Admin";

    public static readonly string[] All = [Student, Teacher, Admin];

    public static readonly string TeacherOrAdmin = $"{Teacher},{Admin}";
}
