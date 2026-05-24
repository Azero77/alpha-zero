using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class removedSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveSessionId",
                table: "TenantUsers");

            migrationBuilder.UpdateData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Statements",
                value: "[{\"Sid\":\"AllowAll\",\"Actions\":[\"*:*\"],\"Effect\":true,\"Condition\":null}]");

            migrationBuilder.UpdateData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "Statements",
                value: "[{\"Sid\":\"AllowViewCourses\",\"Actions\":[\"courses:View\",\"subjects:List\",\"subjects:View\"],\"Effect\":true,\"Condition\":null},{\"Sid\":\"AllowStreaming\",\"Actions\":[\"video:Stream\"],\"Effect\":true,\"Condition\":null},{\"Sid\":\"AllowCompletion\",\"Actions\":[\"enrollments:Complete\"],\"Effect\":true,\"Condition\":null}]");

            migrationBuilder.UpdateData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "Statements",
                value: "[{\"Sid\":\"AllowCourseProduction\",\"Actions\":[\"courses:Create\",\"courses:View\",\"courses:Edit\",\"courses:Submit\"],\"Effect\":true,\"Condition\":null},{\"Sid\":\"AllowVideoUpload\",\"Actions\":[\"video:Upload\",\"video:View\",\"video:List\"],\"Effect\":true,\"Condition\":null}]");

            migrationBuilder.UpdateData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "Statements",
                value: "[{\"Sid\":\"AllowCourseReviewAndEdit\",\"Actions\":[\"courses:View\",\"courses:Edit\",\"courses:Submit\"],\"Effect\":true,\"Condition\":null},{\"Sid\":\"AllowExamManagement\",\"Actions\":[\"exams:Create\",\"exams:Grade\",\"exams:View\"],\"Effect\":true,\"Condition\":null}]");

            migrationBuilder.UpdateData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "Statements",
                value: "[{\"Sid\":\"AllowQAWorkflow\",\"Actions\":[\"courses:View\",\"courses:Approve\",\"courses:Reject\",\"courses:Publish\"],\"Effect\":true,\"Condition\":null}]");

            migrationBuilder.UpdateData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "Statements",
                value: "[{\"Sid\":\"AllowCodeGeneration\",\"Actions\":[\"library:GenerateCodes\",\"library:SellCodes\",\"library:Audit\"],\"Effect\":true,\"Condition\":null}]");

            migrationBuilder.UpdateData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "Statements",
                value: "[{\"Sid\":\"AllowLibraryAudit\",\"Actions\":[\"library:Audit\",\"library:AttachCourses\"],\"Effect\":true,\"Condition\":null}]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActiveSessionId",
                table: "TenantUsers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Statements",
                value: "[{\"Sid\":\"AllowAll\",\"Actions\":[\"*:*\"],\"Effect\":true}]");

            migrationBuilder.UpdateData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "Statements",
                value: "[{\"Sid\":\"AllowViewCourses\",\"Actions\":[\"courses:View\",\"subjects:List\",\"subjects:View\"],\"Effect\":true},{\"Sid\":\"AllowStreaming\",\"Actions\":[\"video:Stream\"],\"Effect\":true},{\"Sid\":\"AllowCompletion\",\"Actions\":[\"enrollments:Complete\"],\"Effect\":true}]");

            migrationBuilder.UpdateData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "Statements",
                value: "[{\"Sid\":\"AllowCourseProduction\",\"Actions\":[\"courses:Create\",\"courses:View\",\"courses:Edit\",\"courses:Submit\"],\"Effect\":true},{\"Sid\":\"AllowVideoUpload\",\"Actions\":[\"video:Upload\",\"video:View\",\"video:List\"],\"Effect\":true}]");

            migrationBuilder.UpdateData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "Statements",
                value: "[{\"Sid\":\"AllowCourseReviewAndEdit\",\"Actions\":[\"courses:View\",\"courses:Edit\",\"courses:Submit\"],\"Effect\":true},{\"Sid\":\"AllowExamManagement\",\"Actions\":[\"exams:Create\",\"exams:Grade\",\"exams:View\"],\"Effect\":true}]");

            migrationBuilder.UpdateData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "Statements",
                value: "[{\"Sid\":\"AllowQAWorkflow\",\"Actions\":[\"courses:View\",\"courses:Approve\",\"courses:Reject\",\"courses:Publish\"],\"Effect\":true}]");

            migrationBuilder.UpdateData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "Statements",
                value: "[{\"Sid\":\"AllowCodeGeneration\",\"Actions\":[\"library:GenerateCodes\",\"library:SellCodes\",\"library:Audit\"],\"Effect\":true}]");

            migrationBuilder.UpdateData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "Statements",
                value: "[{\"Sid\":\"AllowLibraryAudit\",\"Actions\":[\"library:Audit\",\"library:AttachCourses\"],\"Effect\":true}]");
        }
    }
}
