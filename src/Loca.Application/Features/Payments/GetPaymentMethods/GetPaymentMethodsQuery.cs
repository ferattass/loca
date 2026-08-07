using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.Admin.Settings;
using MediatR;

namespace Loca.Application.Features.Payments.GetPaymentMethods;

/// <param name="Instructions">
/// Havale acikken banka bilgileri, kapaliyken <c>null</c>. Ayarlarin tamami
/// donmuyor: arayuzun ihtiyaci olan yalnizca nereye, ne kadar sure icinde
/// odenecegi.
/// </param>
public sealed record OdemeYontemleri(
    bool CardEnabled,
    bool BankTransferEnabled,
    HavaleTalimati? Instructions);

/// <summary>Kullaniciya gosterilecek havale bilgileri.</summary>
/// <remarks>
/// IBAN ve hesap adi sir degil — zaten para gonderilmesi icin paylasiliyor.
/// Saglayici anahtarlari ise bu ucun yanina bile yaklasmiyor.
/// </remarks>
public sealed record HavaleTalimati(
    string BankName,
    string AccountName,
    string Iban,
    int DeadlineHours);

/// <summary>Su an acik olan odeme yontemleri.</summary>
/// <remarks>
/// Ayri bir uc: arayuz "havale dugmesini gostereyim mi" sorusunu ancak
/// sunucuya sorarak cevaplayabilir. Istemcide sabit yazilsaydi panelden
/// havale kapatildiginda dugme durmaya devam eder, basan kullanici hata
/// alirdi.
/// </remarks>
public sealed record GetPaymentMethodsQuery : IRequest<Result<OdemeYontemleri>>;

internal sealed class GetPaymentMethodsQueryHandler(
    IPaymentSettingsReader ayarlar,
    IPaymentService paymentService)
    : IRequestHandler<GetPaymentMethodsQuery, Result<OdemeYontemleri>>
{
    public async Task<Result<OdemeYontemleri>> Handle(
        GetPaymentMethodsQuery request, CancellationToken cancellationToken)
    {
        var odeme = await ayarlar.GetAsync(cancellationToken);
        var havale = odeme.BankTransfer;

        // Banka bilgisi eksikse havale ACIK SAYILMIYOR. Ayar acik ama IBAN
        // bos oldugunda dugmeyi gostermek, kullaniciyi nereye odeyecegini
        // soylemeyen bir ekrana goturmek olurdu.
        var havaleKullanilabilir =
            havale.Enabled
            && !string.IsNullOrWhiteSpace(havale.Iban)
            && !string.IsNullOrWhiteSpace(havale.BankName);

        return Result.Success(new OdemeYontemleri(
            // Kart, calisan bir saglayici oldugu surece acik. Taklit
            // saglayici da bir saglayici: yerelde ve demoda odeme akisinin
            // ucu uca denenebilmesi buna bagli.
            CardEnabled: !string.IsNullOrWhiteSpace(paymentService.Name),
            BankTransferEnabled: havaleKullanilabilir,
            Instructions: havaleKullanilabilir
                ? new HavaleTalimati(
                    havale.BankName,
                    havale.AccountName,
                    havale.Iban,
                    havale.DeadlineHours)
                : null));
    }
}
