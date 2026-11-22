namespace TrustCabinet.Application.Reports;

/// <summary>UC-70b — the same figures as an Excel file. Pure: no storage behind it.</summary>
public interface IMeetingReportRenderer
{
    /// <summary>Renders <paramref name="report"/> as .xlsx bytes, exactly as before.</summary>
    byte[] Build(MeetingReport report);

    /// <summary>Renders one report query table as .xlsx bytes: what was asked, then the groups and the total row.</summary>
    byte[] Build(ReportTable table);
}
