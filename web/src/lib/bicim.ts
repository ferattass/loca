/**
 * Para ve tarih biçimlendirme.
 *
 * <b>Neden tek dosyada:</b> aynı biçimlendiriciler on sekiz ayrı dosyada
 * yeniden kuruluyordu. Kopyalar zamanla ayrışıyor — bir ekran kuruşu
 * gösterip diğeri yuvarlarsa aynı tutar iki yerde farklı okunur ve
 * kullanıcı hangisinin doğru olduğunu bilemez.
 *
 * <b>Kuruşlu / kuruşsuz ayrımı bilinçli, kopya değil.</b> Afişteki fiyat
 * bir başlık ("450 ₺"), ödeme ekranındaki ise tahsil edilecek tutar
 * ("486,00 ₺"). Başlıkta kuruş gürültü, ödemede eksik bilgi.
 *
 * <b>Biçimlendiriciler önbellekte.</b> `Intl.NumberFormat` kurulumu pahalı
 * bir işlem ve dört yerde doğrudan render içinde çağrılıyordu; elli satırlık
 * bir ödeme tablosu her çiziminde elli biçimlendirici kuruyordu. Para
 * birimi kayıttan geldiği için (Money bir ISO kodu taşıyor) sabit bir
 * nesne yetmiyor, birime göre anahtarlanan bir önbellek gerekiyor.
 */

const paraOnbellegi = new Map<string, Intl.NumberFormat>();

function paraBicimlendirici(birim: string, kurusGoster: boolean): Intl.NumberFormat {
  const anahtar = `${birim}:${kurusGoster}`;
  const mevcut = paraOnbellegi.get(anahtar);

  if (mevcut) return mevcut;

  const bicimlendirici = new Intl.NumberFormat('tr-TR', {
    style: 'currency',
    currency: birim,
    ...(kurusGoster ? {} : { maximumFractionDigits: 0 }),
  });

  paraOnbellegi.set(anahtar, bicimlendirici);
  return bicimlendirici;
}

/** Tahsil edilecek tutar — kuruş dahil. Ödeme, rezervasyon, yönetim tabloları. */
export function para(tutar: number, birim = 'TRY'): string {
  return paraBicimlendirici(birim, true).format(tutar);
}

/** Başlık fiyatı — kuruşsuz. Etkinlik kartı, bölüm haritası, özet sayıları. */
export function paraKurussuz(tutar: number, birim = 'TRY'): string {
  return paraBicimlendirici(birim, false).format(tutar);
}

/** 14:30 — yalnızca saat ve dakika. */
export const saatBicimi = new Intl.DateTimeFormat('tr-TR', {
  hour: '2-digit',
  minute: '2-digit',
});

/** 12 Ağu 2026 14:30 — listelerde ve kayıt satırlarında. */
export const tarihSaatBicimi = new Intl.DateTimeFormat('tr-TR', {
  dateStyle: 'medium',
  timeStyle: 'short',
});

/** 12 Ağustos 2026 14:30 — tek bir kaydın öne çıkan tarihi. */
export const uzunTarihSaatBicimi = new Intl.DateTimeFormat('tr-TR', {
  dateStyle: 'long',
  timeStyle: 'short',
});
