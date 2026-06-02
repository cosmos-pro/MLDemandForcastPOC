using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CosmosPro.ML.DemandForCast.Engine.Migrations
{
    /// <inheritdoc />
    public partial class AddSimulacoesCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SimulacoesCompra",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataAgendamento = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DataInicioProcessamento = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DataConclusao = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TreinoJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JanelaDias = table.Column<int>(type: "int", nullable: false),
                    LeadTimeDias = table.Column<int>(type: "int", nullable: false),
                    CicloDias = table.Column<int>(type: "int", nullable: false),
                    FatorServico = table.Column<double>(type: "float", nullable: false),
                    SeriesSimuladas = table.Column<long>(type: "bigint", nullable: true),
                    ResultadoJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MensagemErro = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulacoesCompra", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SimulacoesCompra_Status_DataAgendamento",
                table: "SimulacoesCompra",
                columns: new[] { "Status", "DataAgendamento" });

            migrationBuilder.CreateIndex(
                name: "IX_SimulacoesCompra_TreinoJobId",
                table: "SimulacoesCompra",
                column: "TreinoJobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SimulacoesCompra");
        }
    }
}
