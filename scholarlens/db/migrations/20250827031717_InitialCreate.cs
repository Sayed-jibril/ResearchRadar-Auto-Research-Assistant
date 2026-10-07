using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScholarLens.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "topics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Query = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    YearFrom = table.Column<int>(type: "integer", nullable: true),
                    YearTo = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParametersJson = table.Column<string>(type: "jsonb", nullable: false),
                    HtmlPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PdfPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reports_topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "search_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AuthorsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Abstract = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Doi = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PdfUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    Venue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsOpenAccess = table.Column<bool>(type: "boolean", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    RawJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_search_results_topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paper_texts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SearchResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Tokens = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_texts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paper_texts_search_results_SearchResultId",
                        column: x => x.SearchResultId,
                        principalTable: "search_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "summaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SearchResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    TlDr = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    KeyPointsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Methods = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Results = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LimitationsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_summaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_summaries_search_results_SearchResultId",
                        column: x => x.SearchResultId,
                        principalTable: "search_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_paper_texts_SearchResultId",
                table: "paper_texts",
                column: "SearchResultId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reports_ParametersJson",
                table: "reports",
                column: "ParametersJson")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_reports_TopicId",
                table: "reports",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_search_results_AuthorsJson",
                table: "search_results",
                column: "AuthorsJson")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_search_results_Doi",
                table: "search_results",
                column: "Doi",
                unique: true,
                filter: "doi IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_search_results_RawJson",
                table: "search_results",
                column: "RawJson")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_search_results_TopicId",
                table: "search_results",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_search_results_Year",
                table: "search_results",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_summaries_KeyPointsJson",
                table: "summaries",
                column: "KeyPointsJson")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_summaries_LimitationsJson",
                table: "summaries",
                column: "LimitationsJson")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_summaries_SearchResultId",
                table: "summaries",
                column: "SearchResultId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "paper_texts");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "summaries");

            migrationBuilder.DropTable(
                name: "search_results");

            migrationBuilder.DropTable(
                name: "topics");
        }
    }
}
