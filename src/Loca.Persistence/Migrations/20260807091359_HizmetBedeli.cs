using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loca.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HizmetBedeli : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ServiceFee_Amount",
                table: "Reservations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // defaultValue ELLE "TRY" yapildi; uretilen hâli bos metindi.
            // Money uc harfli ISO kodu sart kosuyor ve bos degerle
            // olusturulamaz: mevcut rezervasyonlarin her okunusu
            // DomainException'a duserdi. Hata da yeni bir kayitta degil,
            // eski kayitlarin goruntulenmesinde cikacagi icin testlerde
            // gorunmez, ancak teslimden sonra ortaya cikardi.
            migrationBuilder.AddColumn<string>(
                name: "ServiceFee_Currency",
                table: "Reservations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "TRY");

            // Varsayilan yalnizca bir tahmin; gercek deger satirin kendi
            // toplamindaki para birimi. Farkli para birimli bir rezervasyon
            // varsa sabit "TRY" onu bozardi ve Money toplama sirasinda
            // "farkli para birimleri islenemez" diye patlardi.
            migrationBuilder.Sql(
                """UPDATE "Reservations" SET "ServiceFee_Currency" = "Total_Currency";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceFee_Amount",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ServiceFee_Currency",
                table: "Reservations");
        }
    }
}
