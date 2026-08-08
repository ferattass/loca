/**
 * Tarayıcının yerel saatini sunucunun beklediği UTC'ye çevirir.
 *
 * `datetime-local` alanı saat dilimi taşımaz; değeri olduğu gibi göndermek,
 * Türkiye'de saat 20:00 diye seçilen etkinliğin sunucuda 20:00 UTC (yani
 * yerel 23:00) olarak kaydedilmesi demek olurdu.
 *
 * <b>Geçersiz tarih burada yakalanıyor.</b> `datetime-local` alanı beş
 * haneli yıl kabul ediyor (tarayıcı "132132" yazılmasına izin veriyor) ve
 * `toISOString()` böyle bir değerde `RangeError` fırlatıyor. Önce bu hata
 * yakalanmıyordu: istek sunucuya hiç gitmeden patlıyor, kullanıcı da
 * sebebini söylemeyen genel bir hata mesajı görüyordu.
 */
export function yereldenUtc(deger: string, alan: string): string {
  const tarih = new Date(deger);

  if (Number.isNaN(tarih.getTime())) {
    throw new Error(`"${alan}" geçerli bir tarih değil.`);
  }

  // ECMAScript tarih araligi ±8.64e15 ms (yaklasik ±275760 yil). Disina
  // cikan deger toISOString'de RangeError firlatir.
  const yil = tarih.getUTCFullYear();

  if (yil < 2000 || yil > 2100) {
    throw new Error(`"${alan}" alanındaki yıl (${yil}) makul aralıkta değil. Örnek: 2027.`);
  }

  return tarih.toISOString();
}
