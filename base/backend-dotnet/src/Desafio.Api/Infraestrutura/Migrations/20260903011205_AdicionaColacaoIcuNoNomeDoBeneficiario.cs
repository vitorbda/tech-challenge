using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desafio.Api.Infraestrutura.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaColacaoIcuNoNomeDoBeneficiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NomeCompleto",
                table: "Beneficiarios",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                collation: "pt-BR-x-icu",
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NomeCompleto",
                table: "Beneficiarios",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldCollation: "pt-BR-x-icu");
        }
    }
}
