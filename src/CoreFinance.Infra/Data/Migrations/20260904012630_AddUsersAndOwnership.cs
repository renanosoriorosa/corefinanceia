using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreFinance.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersAndOwnership : Migration
    {
        private const string UsuarioPadraoId = "11111111-1111-1111-1111-111111111111";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            // Usuário padrão que assume a posse dos registros criados antes do multiusuário.
            // Login: admin@corefinance.local / Senha: corefinance123
            migrationBuilder.Sql($@"
                IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Id] = '{UsuarioPadraoId}')
                INSERT INTO [Users] ([Id], [Name], [Email], [PasswordHash], [Active], [CreatedAt])
                VALUES ('{UsuarioPadraoId}', N'Administrador', N'admin@corefinance.local',
                        N'$2a$11$LscQQ89HSFcGzuuTFyeY0uY0Ibtx/4n1WJsXflPSSdVc3nOAQU5ta', 1, GETUTCDATE());");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid(UsuarioPadraoId));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "FixedAccounts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid(UsuarioPadraoId));

            // O default acima serve apenas para preencher as linhas existentes;
            // a partir daqui quem define o dono é o AppDbContext.
            RemoverDefaultDeUserId(migrationBuilder, "Payments");
            RemoverDefaultDeUserId(migrationBuilder, "FixedAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_UserId",
                table: "Payments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAccounts_UserId",
                table: "FixedAccounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FixedAccounts_Users_UserId",
                table: "FixedAccounts",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        private static void RemoverDefaultDeUserId(MigrationBuilder migrationBuilder, string tabela)
        {
            migrationBuilder.Sql($@"
                DECLARE @constraint NVARCHAR(200);
                SELECT @constraint = d.name
                FROM sys.default_constraints d
                INNER JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
                WHERE d.parent_object_id = OBJECT_ID('{tabela}') AND c.name = 'UserId';
                IF @constraint IS NOT NULL
                    EXEC('ALTER TABLE [{tabela}] DROP CONSTRAINT [' + @constraint + ']');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FixedAccounts_Users_UserId",
                table: "FixedAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Payments_UserId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_FixedAccounts_UserId",
                table: "FixedAccounts");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "FixedAccounts");
        }
    }
}
