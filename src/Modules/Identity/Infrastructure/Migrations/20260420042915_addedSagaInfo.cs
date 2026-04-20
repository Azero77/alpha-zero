using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AlphaZero.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addedSagaInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ManagedPolicies",
                columns: new[] { "Id", "Name", "Statements" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "AdministratorAccess", "[{\"Sid\":\"AllowAll\",\"Actions\":[\"*:*\"],\"Effect\":true}]" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "StudentAccess", "[{\"Sid\":\"AllowViewCourses\",\"Actions\":[\"courses:View\",\"subjects:List\",\"subjects:View\"],\"Effect\":true},{\"Sid\":\"AllowStreaming\",\"Actions\":[\"video:Stream\"],\"Effect\":true},{\"Sid\":\"AllowCompletion\",\"Actions\":[\"enrollments:Complete\"],\"Effect\":true}]" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "CourseWorkerAccess", "[{\"Sid\":\"AllowCourseProduction\",\"Actions\":[\"courses:Create\",\"courses:View\",\"courses:Edit\",\"courses:Submit\"],\"Effect\":true},{\"Sid\":\"AllowVideoUpload\",\"Actions\":[\"video:Upload\",\"video:View\",\"video:List\"],\"Effect\":true}]" },
                    { new Guid("00000000-0000-0000-0000-000000000004"), "TeacherAccess", "[{\"Sid\":\"AllowCourseReviewAndEdit\",\"Actions\":[\"courses:View\",\"courses:Edit\",\"courses:Submit\"],\"Effect\":true},{\"Sid\":\"AllowExamManagement\",\"Actions\":[\"exams:Create\",\"exams:Grade\",\"exams:View\"],\"Effect\":true}]" },
                    { new Guid("00000000-0000-0000-0000-000000000005"), "CourseManagerAccess", "[{\"Sid\":\"AllowQAWorkflow\",\"Actions\":[\"courses:View\",\"courses:Approve\",\"courses:Reject\",\"courses:Publish\"],\"Effect\":true}]" },
                    { new Guid("00000000-0000-0000-0000-000000000006"), "LibraryManagerAccess", "[{\"Sid\":\"AllowCodeGeneration\",\"Actions\":[\"library:GenerateCodes\",\"library:SellCodes\",\"library:Audit\"],\"Effect\":true}]" },
                    { new Guid("00000000-0000-0000-0000-000000000007"), "LibraryAccountantAccess", "[{\"Sid\":\"AllowLibraryAudit\",\"Actions\":[\"library:Audit\",\"library:AttachCourses\"],\"Effect\":true}]" }
                });

            migrationBuilder.InsertData(
                table: "PrincipalTemplates",
                columns: new[] { "Id", "Name", "PrincipalType" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-100000000001"), "Administrator", "Role" },
                    { new Guid("00000000-0000-0000-0000-100000000002"), "Student", "Role" },
                    { new Guid("00000000-0000-0000-0000-100000000003"), "CourseWorker", "Role" },
                    { new Guid("00000000-0000-0000-0000-100000000004"), "Teacher", "Role" },
                    { new Guid("00000000-0000-0000-0000-100000000005"), "TenantCourseManager", "Role" },
                    { new Guid("00000000-0000-0000-0000-100000000006"), "LibraryManager", "Role" },
                    { new Guid("00000000-0000-0000-0000-100000000007"), "LibraryAccountant", "Role" }
                });

            migrationBuilder.InsertData(
                table: "PrincipalManagedPolicyAssignments",
                columns: new[] { "ManagedPolicyId", "PrincipalId" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-100000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-100000000002") },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-100000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-100000000004") },
                    { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-100000000005") },
                    { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-100000000006") },
                    { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-100000000007") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PrincipalManagedPolicyAssignments",
                keyColumns: new[] { "ManagedPolicyId", "PrincipalId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-100000000001") });

            migrationBuilder.DeleteData(
                table: "PrincipalManagedPolicyAssignments",
                keyColumns: new[] { "ManagedPolicyId", "PrincipalId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-100000000002") });

            migrationBuilder.DeleteData(
                table: "PrincipalManagedPolicyAssignments",
                keyColumns: new[] { "ManagedPolicyId", "PrincipalId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-100000000003") });

            migrationBuilder.DeleteData(
                table: "PrincipalManagedPolicyAssignments",
                keyColumns: new[] { "ManagedPolicyId", "PrincipalId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-100000000004") });

            migrationBuilder.DeleteData(
                table: "PrincipalManagedPolicyAssignments",
                keyColumns: new[] { "ManagedPolicyId", "PrincipalId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-100000000005") });

            migrationBuilder.DeleteData(
                table: "PrincipalManagedPolicyAssignments",
                keyColumns: new[] { "ManagedPolicyId", "PrincipalId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-100000000006") });

            migrationBuilder.DeleteData(
                table: "PrincipalManagedPolicyAssignments",
                keyColumns: new[] { "ManagedPolicyId", "PrincipalId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-100000000007") });

            migrationBuilder.DeleteData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "ManagedPolicies",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "PrincipalTemplates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-100000000001"));

            migrationBuilder.DeleteData(
                table: "PrincipalTemplates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-100000000002"));

            migrationBuilder.DeleteData(
                table: "PrincipalTemplates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-100000000003"));

            migrationBuilder.DeleteData(
                table: "PrincipalTemplates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-100000000004"));

            migrationBuilder.DeleteData(
                table: "PrincipalTemplates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-100000000005"));

            migrationBuilder.DeleteData(
                table: "PrincipalTemplates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-100000000006"));

            migrationBuilder.DeleteData(
                table: "PrincipalTemplates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-100000000007"));
        }
    }
}
