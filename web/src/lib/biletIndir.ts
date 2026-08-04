import { toPng } from 'html-to-image';

/**
 * Indirilen gorselin zemin rengi.
 *
 * Bilet acik renkli bir "kagit"; kenarindaki perforasyon centikleri
 * arkadaki zemini gosterdigi icin o zeminin ne oldugu bilinmek zorunda.
 * Uygulamanin tek temasi var (koyu), bu yuzden sabit: centikler ekranda
 * ne gorunuyorsa indirilen dosyada da ayni gorunur.
 */
const ZEMIN_RENGI = '#15121b';

/** Ekran cozunurlugunun katı; 3x, A4'e basildiginda QR'in okunur kalmasi icin. */
const OLCEK = 3;

/**
 * Dosya adinda kullanilamayacak karakterleri temizler.
 *
 * Bilet numarasi bizim urettigimiz kisitli bir alfabeden geliyor, yani
 * pratikte zaten guvenli. Yine de dosya adi isletim sistemine giden bir
 * deger: kaynagina guvenip gecmek, kaynagin bir gun degismesi durumunda
 * sessizce bozulan bir yol acardi.
 */
function dosyaAdiTemizle(ad: string): string {
  return ad.replace(/[^\w.-]+/g, '-').replace(/^-+|-+$/g, '') || 'bilet';
}

/**
 * Bilet dugumunu PNG'ye cevirir.
 *
 * <b>Iki kez cagriliyor.</b> html-to-image dugumu bir SVG
 * <c>foreignObject</c> icine kopyalayip goruntuye cizdiriyor; ilk cagrida
 * self-host edilen yazi tipleri kopya belgede henuz cozulmemis oluyor ve
 * bilet varsayilan fontla cikabiliyor. Ikinci cagri sicak onbellekle
 * calisiyor. Maliyeti bir kullanici tiklamasi basina birkac yuz
 * milisaniye; karsiliginda yanlis fontla inen bir bilet olmuyor.
 */
async function gorseleCevir(dugum: HTMLElement): Promise<string> {
  const secenekler = {
    pixelRatio: OLCEK,
    backgroundColor: ZEMIN_RENGI,
    cacheBust: true,
  };

  await toPng(dugum, secenekler);

  return toPng(dugum, secenekler);
}

function indir(veriUrl: string, dosyaAdi: string): void {
  const baglanti = document.createElement('a');
  baglanti.href = veriUrl;
  baglanti.download = dosyaAdi;
  baglanti.click();
}

export async function biletiGorselIndir(dugum: HTMLElement, biletNo: string): Promise<void> {
  const veriUrl = await gorseleCevir(dugum);

  indir(veriUrl, `${dosyaAdiTemizle(biletNo)}.png`);
}

/**
 * Bileti A4 sayfanin ustune yerlestirip PDF olarak indirir.
 *
 * <para>
 * Sayfa boyutu biletin kendi olculerine degil A4'e ayarlandi: bilet
 * boyutunda ozel bir sayfa telefonda daha sik gorunurdu ama yazicidan
 * cikarmak isteyen kullanici her seferinde olcek ayari yapmak zorunda
 * kalirdi. Bilet zaten sayfanin ustunde, altta kalan bosluk katlama payi.
 * </para>
 *
 * <para>
 * <c>jspdf</c> dinamik olarak yukleniyor: yaklasik 350 KB'lik bir paket
 * ve yalnizca bu dugmeye basildiginda gerekiyor; ana pakete girseydi
 * bileti hic indirmeyen kullanici da her ziyarette indirmis olurdu.
 * </para>
 */
export async function biletiPdfIndir(dugum: HTMLElement, biletNo: string): Promise<void> {
  const [veriUrl, { jsPDF }] = await Promise.all([gorseleCevir(dugum), import('jspdf')]);

  const pdf = new jsPDF({ unit: 'pt', format: 'a4', orientation: 'portrait' });

  const kenarBosluk = 36;
  const sayfaGenisligi = pdf.internal.pageSize.getWidth();
  const genislik = sayfaGenisligi - kenarBosluk * 2;

  // Oran DOM'dan okunuyor, goruntuden degil: goruntu OLCEK katiyla
  // buyutuldugu icin piksel degerleri sayfa birimiyle ayni sey degil.
  const yukseklik = (dugum.offsetHeight / dugum.offsetWidth) * genislik;

  pdf.addImage(veriUrl, 'PNG', kenarBosluk, kenarBosluk, genislik, yukseklik);
  pdf.save(`${dosyaAdiTemizle(biletNo)}.pdf`);
}
