using System;
using System.Collections;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Courses");

            migrationBuilder.CreateTable(
                name: "CourseAnalytics",
                schema: "Courses",
                columns: table => new
                {
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalEnrollments = table.Column<int>(type: "integer", nullable: false),
                    SumOfCompletionPercentages = table.Column<double>(type: "double precision", nullable: false),
                    ItemCompletions = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseAnalytics", x => x.CourseId);
                });

            migrationBuilder.CreateTable(
                name: "CourseRedemptionStates",
                schema: "Courses",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseArn = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Plan = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuthorizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseRedemptionStates", x => x.CorrelationId);
                });

            migrationBuilder.CreateTable(
                name: "CourseRevocationStates",
                schema: "Courses",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentState = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceArn = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnauthorizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseRevocationStates", x => x.CorrelationId);
                });

            migrationBuilder.CreateTable(
                name: "Courses",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    NextAvailableBitIndex = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    OnDeleted = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Enrollements",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ProgressBitmask = table.Column<BitArray>(type: "varbit", nullable: false),
                    ProgressTotalItems = table.Column<int>(type: "integer", nullable: false),
                    EnrolledOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    OnDeleted = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoursePlans",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PrincipalId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoursePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoursePlans_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "Courses",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseSections",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    OnDeleted = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseSections_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "Courses",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumItems",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    BitIndex = table.Column<int>(type: "integer", nullable: false),
                    MainType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    OnDeleted = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumItems_CourseSections_SectionId",
                        column: x => x.SectionId,
                        principalSchema: "Courses",
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumResources",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Arn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Metadata = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    CurriculumItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumResources_CurriculumItems_CurriculumItemId",
                        column: x => x.CurriculumItemId,
                        principalSchema: "Courses",
                        principalTable: "CurriculumItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseAnalytics_CourseId",
                schema: "Courses",
                table: "CourseAnalytics",
                column: "CourseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoursePlans_CourseId_Name",
                schema: "Courses",
                table: "CoursePlans",
                columns: new[] { "CourseId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courses_IsDeleted",
                schema: "Courses",
                table: "Courses",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_CourseId",
                schema: "Courses",
                table: "CourseSections",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_IsDeleted",
                schema: "Courses",
                table: "CourseSections",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumItems_IsDeleted",
                schema: "Courses",
                table: "CurriculumItems",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumItems_SectionId",
                schema: "Courses",
                table: "CurriculumItems",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumResources_CurriculumItemId",
                schema: "Courses",
                table: "CurriculumResources",
                column: "CurriculumItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollements_StudentId_CourseId",
                schema: "Courses",
                table: "Enrollements",
                columns: new[] { "StudentId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_IsDeleted",
                schema: "Courses",
                table: "Subjects",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseAnalytics",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "CoursePlans",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "CourseRedemptionStates",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "CourseRevocationStates",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "CurriculumResources",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "Enrollements",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "Subjects",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "CurriculumItems",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "CourseSections",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "Courses",
                schema: "Courses");
        }
    }
}
