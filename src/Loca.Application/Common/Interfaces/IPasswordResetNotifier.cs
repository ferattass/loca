namespace Loca.Application.Common.Interfaces;

/// <summary>
/// Sifirlama token'ini kullaniciya ulastirir.
/// </summary>
/// <remarks>
/// Handler'in token'i dogrudan loglamamasi icin var. Loglasaydi token
/// uretim log'larina da duserdi ve log dosyasini goren herkes istedigi
/// hesabin sifresini degistirebilirdi.
///
/// <para>
/// Gun 9'a kadar tek uygulamasi token'i gelistirme log'una yazar; e-posta
/// altyapisi geldiginde ayni arayuzun SMTP uygulamasi devreye girecek ve
/// handler'lar degismeyecek.
/// </para>
/// </remarks>
public interface IPasswordResetNotifier
{
    Task SendAsync(
        string email,
        string token,
        DateTime expiresAt,
        CancellationToken cancellationToken = default);
}
