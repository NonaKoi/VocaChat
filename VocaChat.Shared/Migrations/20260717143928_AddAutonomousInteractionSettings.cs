using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VocaChat.Migrations
{
    /// <inheritdoc />
    public partial class AddAutonomousInteractionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutonomousInteractionSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Frequency = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AllowPrivateChats = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowGroupChats = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutonomousInteractionSettings", x => x.Id);
                    table.CheckConstraint("CK_AutonomousInteractionSettings_Frequency", "\"Frequency\" IN ('Low', 'Normal', 'High')");
                    table.CheckConstraint("CK_AutonomousInteractionSettings_Singleton", "\"Id\" = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutonomousInteractionSettings");
        }
    }
}
