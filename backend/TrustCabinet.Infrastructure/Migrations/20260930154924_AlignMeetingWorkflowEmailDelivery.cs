using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations;

/// <summary>Aligns the combined model of the independently developed SCRUM-15 and SCRUM-19 migrations.</summary>
public partial class AlignMeetingWorkflowEmailDelivery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The workflow and reliable-email migrations already create every column, FK and index.
        // This merge marker synchronizes their combined snapshot without applying duplicate DDL.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Schema rollback remains owned by the two original migrations.
    }
}
