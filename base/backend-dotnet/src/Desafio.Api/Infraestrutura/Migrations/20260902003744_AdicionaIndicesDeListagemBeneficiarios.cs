using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desafio.Api.Infraestrutura.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaIndicesDeListagemBeneficiarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Beneficiarios_NomeCompleto_Id",
                table: "Beneficiarios",
                columns: new[] { "NomeCompleto", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiarios_Status",
                table: "Beneficiarios",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Beneficiarios_NomeCompleto_Id",
                table: "Beneficiarios");

            migrationBuilder.DropIndex(
                name: "IX_Beneficiarios_Status",
                table: "Beneficiarios");
        }
    }
}
