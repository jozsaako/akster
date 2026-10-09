using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetSitting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdditiveRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SitterAvailabilities -> SitterProfiles, keeping the rows.
            migrationBuilder.RenameTable(name: "SitterAvailabilities", newName: "SitterProfiles");
            migrationBuilder.Sql("EXEC sp_rename N'PK_SitterAvailabilities', N'PK_SitterProfiles', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_SitterAvailabilities_Users_UserId', N'FK_SitterProfiles_Users_UserId', N'OBJECT';");
            migrationBuilder.RenameIndex(name: "IX_SitterAvailabilities_UserId", table: "SitterProfiles", newName: "IX_SitterProfiles_UserId");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "SitterProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOwner",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Data fix: old Role=Sitter (1) users become active sitters and stop being owners.
            // Sitters who never saved a profile have no row and stay inactive.
            migrationBuilder.Sql("EXEC(N'UPDATE SitterProfiles SET IsActive = 1 WHERE UserId IN (SELECT Id FROM Users WHERE Role = 1)');");
            migrationBuilder.Sql("EXEC(N'UPDATE Users SET IsOwner = 0 WHERE Role = 1');");

            migrationBuilder.DropColumn(name: "Role", table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Best effort: active sitters that are not owners go back to Sitter.
            migrationBuilder.Sql("EXEC(N'UPDATE Users SET Role = 1 WHERE IsOwner = 0 AND Id IN (SELECT UserId FROM SitterProfiles WHERE IsActive = 1)');");

            migrationBuilder.DropColumn(name: "IsOwner", table: "Users");
            migrationBuilder.DropColumn(name: "IsActive", table: "SitterProfiles");

            migrationBuilder.RenameIndex(name: "IX_SitterProfiles_UserId", table: "SitterProfiles", newName: "IX_SitterAvailabilities_UserId");
            migrationBuilder.Sql("EXEC sp_rename N'FK_SitterProfiles_Users_UserId', N'FK_SitterAvailabilities_Users_UserId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'PK_SitterProfiles', N'PK_SitterAvailabilities', N'OBJECT';");
            migrationBuilder.RenameTable(name: "SitterProfiles", newName: "SitterAvailabilities");
        }
    }
}
