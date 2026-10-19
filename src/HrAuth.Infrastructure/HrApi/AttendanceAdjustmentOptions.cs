namespace HrAuth.Infrastructure.HrApi;

/// <summary>Settings for the attendance-adjustment HR API.</summary>
public sealed class AttendanceAdjustmentOptions
{
    public const string SectionName = "AttendanceAdjustment";

    /// <summary>Base URL of the HR API instance handling adjustments.</summary>
    public string BaseUrl { get; init; } = "https://hrapi.qmlab.link";

    public int TimeoutSeconds { get; init; } = 30;
}
