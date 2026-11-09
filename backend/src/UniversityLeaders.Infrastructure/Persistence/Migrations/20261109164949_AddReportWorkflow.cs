using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityLeaders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "activity_date",
                table: "reports",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "outcome",
                table: "reports",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "participant_count",
                table: "reports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "score",
                table: "report_reviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "score_breakdown",
                table: "report_reviews",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_reports_participant_count",
                table: "reports",
                sql: "participant_count IS NULL OR participant_count >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reports_score_when_approved",
                table: "reports",
                sql: "(status = 'Approved') = (score IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_report_reviews_rejection_comment",
                table: "report_reviews",
                sql: "action <> 'Rejected' OR comment IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_report_reviews_score",
                table: "report_reviews",
                sql: "score IS NULL OR score BETWEEN 0 AND 10");

            migrationBuilder.AddCheckConstraint(
                name: "ck_report_reviews_score_when_approved",
                table: "report_reviews",
                sql: "(action = 'Approved') = (score IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_reports_participant_count",
                table: "reports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reports_score_when_approved",
                table: "reports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_report_reviews_rejection_comment",
                table: "report_reviews");

            migrationBuilder.DropCheckConstraint(
                name: "ck_report_reviews_score",
                table: "report_reviews");

            migrationBuilder.DropCheckConstraint(
                name: "ck_report_reviews_score_when_approved",
                table: "report_reviews");

            migrationBuilder.DropColumn(
                name: "activity_date",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "outcome",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "participant_count",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "score",
                table: "report_reviews");

            migrationBuilder.DropColumn(
                name: "score_breakdown",
                table: "report_reviews");
        }
    }
}
