namespace Loca.Application.Common.Interfaces;

/// <summary>
/// Outbox mesajini gercek hedefine gonderir (e-posta, bildirim, webhook).
/// </summary>
/// <remarks>
/// Gonderim isi arayuz arkasinda: bugun log'a yaziliyor, Gun 9'da SMTP
/// uygulamasi gelecek ve outbox isini calistiran kod degismeyecek. Ayni
/// yaklasim <c>IPasswordResetNotifier</c> icin de kullanildi.
///
/// <para>
/// <b>Gonderim tekrarlanabilir olmali.</b> Outbox "en az bir kez" teslim
/// garantisi veriyor: isleyen taraf mesaji gonderip isaretlemeden cokerse
/// mesaj tekrar islenir. Ayni bildirimin iki kez gitmesi kabul edilebilir,
/// hic gitmemesi degil.
/// </para>
/// </remarks>
public interface IOutboxDispatcher
{
    Task DispatchAsync(string type, string payload, CancellationToken cancellationToken = default);
}
