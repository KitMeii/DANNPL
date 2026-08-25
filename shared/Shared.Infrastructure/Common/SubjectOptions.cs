namespace Shared.Infrastructure.Common;

/// <summary>Bound from config section "Subject". Central place naming the subject/school this
/// platform instance is configured for — every AI prompt, seed string, or UI text that needs to
/// mention them reads from here instead of hardcoding a specific course name, so retargeting the
/// platform to a different subject is a one-line config change.</summary>
public sealed class SubjectOptions
{
    public const string SectionName = "Subject";

    public string SubjectName { get; init; } = "Môn học";
    public string SchoolName { get; init; } = "Học viện";
}
