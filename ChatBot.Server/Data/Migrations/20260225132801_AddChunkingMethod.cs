using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatBot.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkingMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChunkingMethod",
                table: "DocumentChunks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChunkingMethod",
                table: "DocumentChunks");
        }
    }
}
