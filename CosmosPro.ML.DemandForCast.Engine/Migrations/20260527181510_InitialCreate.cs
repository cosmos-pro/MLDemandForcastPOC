using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CosmosPro.ML.DemandForCast.Engine.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CargasStage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataAgendamento = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DataInicioProcessamento = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DataConclusao = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NomeArquivoOriginal = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    BlobKey = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    MensagemErro = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LinhasImportadas = table.Column<long>(type: "bigint", nullable: true),
                    UsuarioId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargasStage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TreinoJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataAgendamento = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DataInicioProcessamento = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DataConclusao = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MaxSkus = table.Column<int>(type: "int", nullable: false),
                    ModeloBlobKey = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ResultadoJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FeaturesGeradas = table.Column<long>(type: "bigint", nullable: true),
                    MensagemErro = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreinoJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CargasStage_Status_DataAgendamento",
                table: "CargasStage",
                columns: new[] { "Status", "DataAgendamento" });

            migrationBuilder.CreateIndex(
                name: "IX_TreinoJobs_Status_DataAgendamento",
                table: "TreinoJobs",
                columns: new[] { "Status", "DataAgendamento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CargasStage");

            migrationBuilder.DropTable(
                name: "TreinoJobs");
        }
    }
}
