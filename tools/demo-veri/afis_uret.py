# -*- coding: utf-8 -*-
"""Demo etkinlikleri icin okunabilir afis uretir.

Ilk surumde afis tek renk bir PNG'di, sonra degradeye cevrildi; ikisi de
vitrinde BOS bir renk blogu olarak gorunuyordu. Afis yayina almanin
domain on kosulu oldugu icin (Event.EnsureReadyForPublish) kaldirmak da
mumkun degil — bu yuzden afisin gercekten bir sey soylemesi saglandi:
etkinligin adi, tarihi, mekani ve kategorisi afisin uzerine yaziliyor.

Pillow ISTEGE BAGLI. Kurulu degilse yazisiz degrade uretiliyor ve betik
yine calisiyor; depoya zorunlu bir bagimlilik eklenmedi cunku bu yalnizca
demo verisi hazirlayan bir gelistirme araci.

    pip install pillow
"""

import hashlib
import os
import struct
import urllib.request
import zlib

GENISLIK = 600
YUKSEKLIK = 800

# Her etkinlige bir palet: (ust renk, alt renk, vurgu).
PALETLER = [
    ((88, 28, 135), (17, 24, 39), (236, 72, 153)),
    ((12, 74, 110), (15, 23, 42), (56, 189, 248)),
    ((124, 45, 18), (28, 25, 23), (251, 146, 60)),
    ((6, 78, 59), (15, 23, 42), (52, 211, 153)),
    ((131, 24, 67), (24, 24, 27), (244, 114, 182)),
    ((30, 27, 75), (15, 23, 42), (129, 140, 248)),
]

# Turkce karakter tasiyan yaygin yazi tipleri; ilk bulunan kullaniliyor.
YAZI_TIPLERI_BASLIK = [
    r"C:\Windows\Fonts\seguibl.ttf",
    r"C:\Windows\Fonts\ariblk.ttf",
    r"C:\Windows\Fonts\segoeuib.ttf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
]

YAZI_TIPLERI = [
    r"C:\Windows\Fonts\segoeuib.ttf",
    r"C:\Windows\Fonts\arialbd.ttf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
]

YAZI_TIPLERI_INCE = [
    r"C:\Windows\Fonts\segoeui.ttf",
    r"C:\Windows\Fonts\arial.ttf",
    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
]


def karistir(a, b, oran):
    """Iki rengi oranla karistirir. oran=0 → a, oran=1 → b."""
    return tuple(round(a[i] + (b[i] - a[i]) * oran) for i in range(3))


def _zemin(palet, tohum):
    """Dikey degrade + capraz seritler + alt perde. Piksel piksel."""
    ust, alt, vurgu = palet
    satirlar = []

    for y in range(YUKSEKLIK):
        dikey = y / (YUKSEKLIK - 1)
        temel = karistir(ust, alt, dikey)
        piksel = bytearray()

        for x in range(GENISLIK):
            renk = temel

            # Capraz seritler; tohum her afiste deseni kaydiriyor.
            konum = (x + y * 2 + tohum * 97) % 420

            if konum < 6:
                renk = karistir(temel, vurgu, 0.55)
            elif konum < 12:
                renk = karistir(temel, vurgu, 0.22)

            uzaklik = ((x - GENISLIK * 0.25) ** 2 + (y - YUKSEKLIK * 0.18) ** 2) ** 0.5
            if uzaklik < 320:
                renk = karistir(renk, vurgu, 0.18 * (1 - uzaklik / 320))

            # Alt perde: uzerine gelen yazi okunur kalsin.
            if dikey > 0.52:
                renk = karistir(renk, (10, 10, 14), (dikey - 0.52) / 0.48 * 0.86)

            piksel += bytes(renk)

        satirlar.append(b"\x00" + bytes(piksel))

    return b"".join(satirlar)


def _png(ham):
    def parca(tur, veri):
        return (
            struct.pack(">I", len(veri))
            + tur
            + veri
            + struct.pack(">I", zlib.crc32(tur + veri) & 0xFFFFFFFF)
        )

    return (
        b"\x89PNG\r\n\x1a\n"
        + parca(b"IHDR", struct.pack(">IIBBBBB", GENISLIK, YUKSEKLIK, 8, 2, 0, 0, 0))
        + parca(b"IDAT", zlib.compress(ham, 9))
        + parca(b"IEND", b"")
    )


def _yazi_tipi(adaylar, boyut):
    from PIL import ImageFont

    for yol in adaylar:
        try:
            return ImageFont.truetype(yol, boyut)
        except OSError:
            continue

    # Hicbiri yoksa PIL'in gomulu bitmap yazi tipi. Cirkin ama okunur ve
    # betigin hic calismamasindan iyi.
    return ImageFont.load_default()


def _sar(cizim, metin, yazi, genislik):
    """Metni verilen genislige sigacak satirlara boler."""
    kelimeler = metin.split()
    satirlar = []
    satir = ""

    for kelime in kelimeler:
        deneme = f"{satir} {kelime}".strip()

        if cizim.textlength(deneme, font=yazi) <= genislik:
            satir = deneme
        else:
            if satir:
                satirlar.append(satir)
            satir = kelime

    if satir:
        satirlar.append(satir)

    return satirlar


# Afis iki kat cozunurlukte cizilip kucultuluyor. Dogrudan 600x800
# cizildiginde yazi kenarlari kirik cikiyordu; kucultme kenar
# yumusatmasini bedavaya getiriyor.
OLCEK = 2

# Fotograf kaynagi. Tohum basliktan turetiliyor: ayni etkinlik her
# kosumda AYNI fotografi aliyor, katalog dagitimdan dagitima
# degismiyor. Rastgele olsaydi her afis yenilemede vitrin bastan
# baslardi ve ekran goruntuleri tutmazdi.
FOTOGRAF_ADRESI = "https://picsum.photos/seed/{tohum}/{genislik}/{yukseklik}"

# Ag yoksa ya da kapatilmak istenirse: LOCA_AFIS_FOTOGRAF=0
FOTOGRAF_ACIK = os.environ.get("LOCA_AFIS_FOTOGRAF", "1") != "0"


def _fotograf_indir(tohum_metni, genislik, yukseklik):
    """Fotografi indirir; basarisiz olursa None doner (afis yine uretilir)."""
    if not FOTOGRAF_ACIK:
        return None

    # Tohum ASCII'ye indirgeniyor: adres bileseni olarak gidiyor ve
    # Turkce harfler kaynakta farkli kodlanip her kosumda baska bir
    # fotograf getirebilirdi.
    tohum = hashlib.sha1(tohum_metni.encode("utf-8")).hexdigest()[:12]

    adres = FOTOGRAF_ADRESI.format(tohum=tohum, genislik=genislik, yukseklik=yukseklik)

    try:
        with urllib.request.urlopen(adres, timeout=30) as cevap:
            return cevap.read()
    except Exception:
        # Afis uretimi ag yuzunden HIC durmamali: fotograf yoksa
        # degrade zemin kullaniliyor, yazilar aynen basiliyor.
        return None


def _duotone(gorsel, koyu, vurgu):
    """Fotografi paletin iki rengi arasina esler.

    Fotograflar birbirinden bagimsiz geliyor; oldugu gibi kullanilsalarsa
    katalog rastgele bir fotograf yigini gibi gorunur. Tek renk ekseni
    uzerine oturtulunca hepsi ayni tasarim dilinden konusuyor ve konusu
    ne olursa olsun afis gibi duruyor.
    """
    from PIL import Image

    gri = gorsel.convert("L")

    tablo = []
    for kanal in range(3):
        tablo += [round(koyu[kanal] + (vurgu[kanal] - koyu[kanal]) * (i / 255))
                  for i in range(256)]

    return Image.merge("RGB", (gri, gri, gri)).point(tablo)


def _karartma(boyut, vurgu):
    """Alttan yukari koyulasan perde: yazinin okunmasi buna bagli.

    Duz yariseffaf bir katman da okunurlugu saglardi ama fotografi
    tamamen olduruyordu. Degrade, ustte fotografi birakip altta yaziya
    yer aciyor.
    """
    from PIL import Image

    genislik, yukseklik = boyut
    perde = Image.new("L", (1, yukseklik))

    for y in range(yukseklik):
        oran = y / (yukseklik - 1)

        if oran < 0.32:
            saydam = 60 * (oran / 0.32)          # ustte hafif
        else:
            t = (oran - 0.32) / 0.68
            saydam = 60 + 195 * (t ** 1.6)       # altta neredeyse tam

        perde.putpixel((0, y), round(min(255, saydam)))

    return perde.resize((genislik, yukseklik))


def afis_uret(palet_no, tohum, baslik, tarih_metni, mekan, kategori):
    """Afisi uretir; Pillow yoksa yazisiz degrade doner."""
    palet = PALETLER[palet_no % len(PALETLER)]
    zemin = _zemin(palet, tohum)
    ham = _png(zemin)

    try:
        import io

        from PIL import Image, ImageDraw, ImageFilter
    except ImportError:
        return ham

    G = GENISLIK * OLCEK
    Y = YUKSEKLIK * OLCEK
    koyu, _alt, vurgu = palet

    fotograf = _fotograf_indir(baslik, G, Y)

    if fotograf:
        try:
            arka = Image.open(io.BytesIO(fotograf)).convert("RGB")
            arka = arka.resize((G, Y), Image.LANCZOS)
            # BULANIKLIK YUKSEK VE BILINCLI. Fotograflar konuya gore
            # secilemiyor: anahtar kelimeyle arama yapan ucretsiz
            # kaynaklar ya gorselin ustune lisans damgasi basiyor ya da
            # turev calismaya izin vermeyen lisanslar donuyor (cc-nc-nd).
            # Kaynak bu yuzden konudan bagimsiz; nesneler secilir hâlde
            # kalsaydi rock konserinin afisinde kupa, cocuk oyununda
            # catal gorunurdu. Bu kadar bulaniklikta fotograf bir nesne
            # olmaktan cikip isik ve doku oluyor, kompozisyon kaliyor.
            arka = arka.filter(ImageFilter.GaussianBlur(radius=OLCEK * 14))
            arka = _duotone(arka, koyu, vurgu)

            # Dokuyu geri getiren ince tanecik: yalnizca bulanik degrade
            # kalsaydi afis yeniden "duz renk blogu"na donerdi — ilk
            # surumun tam olarak dustugu yer.
            arka = Image.blend(
                arka, arka.filter(ImageFilter.EMBOSS).convert("RGB"), 0.06)
        except Exception:
            fotograf = None

    if not fotograf:
        arka = Image.open(io.BytesIO(ham)).convert("RGB").resize((G, Y), Image.LANCZOS)

    # Perde ayri bir katman olarak biniyor; dogrudan piksel piksel
    # karartmak ayni sonucu verirdi ama 1200x1600'de gozle gorulur
    # yavastir.
    siyah = Image.new("RGB", (G, Y), (8, 8, 12))
    gorsel = Image.composite(siyah, arka, _karartma((G, Y), vurgu))

    cizim = ImageDraw.Draw(gorsel)

    kenar = 52 * OLCEK
    ic_genislik = G - kenar * 2

    # --- Kategori rozeti -------------------------------------------------
    kucuk = _yazi_tipi(YAZI_TIPLERI, 20 * OLCEK)
    etiket = kategori.upper()
    metin_en = cizim.textlength(etiket, font=kucuk)
    dolgu_x, dolgu_y = 14 * OLCEK, 8 * OLCEK

    cizim.rounded_rectangle(
        [kenar, kenar, kenar + metin_en + dolgu_x * 2, kenar + 22 * OLCEK + dolgu_y * 2],
        radius=6 * OLCEK, fill=vurgu)
    cizim.text((kenar + dolgu_x, kenar + dolgu_y - 2 * OLCEK), etiket,
               font=kucuk, fill=(12, 10, 18))

    # --- Baslik ----------------------------------------------------------
    #
    # Punto icerige gore kuculuyor: sabit puntoda uzun basliklar dort bes
    # satira yayilip afisin yarisini kapliyordu.
    for punto in (52, 46, 40, 35):
        buyuk = _yazi_tipi(YAZI_TIPLERI_BASLIK, punto * OLCEK)
        satirlar = _sar(cizim, baslik, buyuk, ic_genislik)
        if len(satirlar) <= 3:
            break

    orta = _yazi_tipi(YAZI_TIPLERI, 22 * OLCEK)
    ince = _yazi_tipi(YAZI_TIPLERI_INCE, 21 * OLCEK)

    y = Y - kenar - 30 * OLCEK
    cizim.text((kenar, y), mekan, font=ince, fill=(196, 196, 210))

    y -= 38 * OLCEK
    cizim.text((kenar, y), tarih_metni, font=orta, fill=vurgu)

    y -= 20 * OLCEK
    for satir in reversed(satirlar):
        y -= round(punto * 1.16) * OLCEK
        cizim.text((kenar, y), satir, font=buyuk, fill=(255, 255, 255))

    # Basligin ustunde ince vurgu cizgisi: goz once buraya takiliyor ve
    # afis "yazi yigini" olmaktan cikiyor.
    y -= 26 * OLCEK
    cizim.rounded_rectangle(
        [kenar, y, kenar + 64 * OLCEK, y + 5 * OLCEK],
        radius=3 * OLCEK, fill=vurgu)

    gorsel = gorsel.resize((GENISLIK, YUKSEKLIK), Image.LANCZOS)

    cikti = io.BytesIO()
    gorsel.save(cikti, format="PNG", optimize=True)

    return cikti.getvalue()
