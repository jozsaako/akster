using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetSitting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Location_City", table: "Users", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Location_County", table: "Users", type: "nvarchar(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<double>(name: "Location_Latitude", table: "Users", type: "float", nullable: true);
            migrationBuilder.AddColumn<double>(name: "Location_Longitude", table: "Users", type: "float", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Location_PostalCode", table: "Users", type: "nvarchar(6)", maxLength: 6, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Location_Street", table: "Users", type: "nvarchar(200)", maxLength: 200, nullable: true);

            // Keep the legacy free-text address as the street; county, city and postal code stay empty
            // (and the location un-geocoded) until the user saves a structured location.
            migrationBuilder.Sql("EXEC(N'UPDATE Users SET Location_Street = LEFT(LTRIM(RTRIM(Address)), 200) WHERE Address IS NOT NULL AND LTRIM(RTRIM(Address)) <> N''''');");
            migrationBuilder.DropColumn(name: "Address", table: "Users");

            migrationBuilder.CreateTable(
                name: "Localities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    County = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CityKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Localities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Location_Latitude_Location_Longitude",
                table: "Users",
                columns: new[] { "Location_Latitude", "Location_Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Localities_County_CityKey",
                table: "Localities",
                columns: new[] { "County", "CityKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Localities_PostalCode",
                table: "Localities",
                column: "PostalCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Localities");

            migrationBuilder.AddColumn<string>(name: "Address", table: "Users", type: "nvarchar(max)", nullable: true);
            migrationBuilder.Sql("EXEC(N'UPDATE Users SET Address = Location_Street');");

            migrationBuilder.DropIndex(name: "IX_Users_Location_Latitude_Location_Longitude", table: "Users");
            migrationBuilder.DropColumn(name: "Location_City", table: "Users");
            migrationBuilder.DropColumn(name: "Location_County", table: "Users");
            migrationBuilder.DropColumn(name: "Location_Latitude", table: "Users");
            migrationBuilder.DropColumn(name: "Location_Longitude", table: "Users");
            migrationBuilder.DropColumn(name: "Location_PostalCode", table: "Users");
            migrationBuilder.DropColumn(name: "Location_Street", table: "Users");
        }
    }
}
