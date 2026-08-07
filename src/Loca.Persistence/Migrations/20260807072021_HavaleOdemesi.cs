using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loca.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HavaleOdemesi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue ELLE 1 (PaymentMethod.Card) yapildi; uretilen hâli
            // 0'di. Enum 1'den basliyor, yani 0 hicbir yontemin karsiligi
            // degil: mevcut odemeler tanimsiz bir degerle kalir ve "havale
            // mi" sorusu her yerde false donerken listede yontem sutunu bos
            // gorunurdu. Sutun eklendiginde veritabanindaki her odeme kartla
            // yapilmisti, dogru varsayilan Card.
            migrationBuilder.AddColumn<int>(
                name: "Method",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Method_Status",
                table: "Payments",
                columns: new[] { "Method", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_Method_Status",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "Payments");
        }
    }
}
