namespace TrustCabinet.Application.Reports;

/// <summary>UC-70b — the same figures as an Excel file. Pure: no storage behind it.</summary>
public interface IMeetingReportRenderer
{
    /// <summary>Renders <paramref name="report"/> as .xlsx bytes, exactly as before.</summary>
    byte[] Build(MeetingReport report);
}
