using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loca.Persistence.Migrations
{
    /// <summary>
    /// Bilet satirina eszamanlilik damgasi.
    /// </summary>
    /// <remarks>
    /// <b>Bilerek BOS.</b> <c>Ticket.Version</c> alani PostgreSQL'in
    /// <c>xmin</c> sistem kolonuna esleniyor; o kolon her tabloda
    /// kendiliginden var. Uretilen migration onu normal bir kolon gibi
    /// eklemeye calisiyordu ve PostgreSQL bunu reddediyor:
    /// "column name xmin conflicts with a system column name".
    ///
    /// <para>
    /// Ayni tuzak Gun 4'te EventSeat icin de yasandi (bkz.
    /// <c>20260730063045_Etkinlik</c>). Migration silinmiyor cunku model
    /// snapshot'i degisti; snapshot ile migration zinciri arasinda bosluk
    /// kalirsa bir sonraki "migrations add" ayni satiri tekrar uretir.
    /// </para>
    /// </remarks>
    public partial class BiletOkutma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
