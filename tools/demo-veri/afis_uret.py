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

import struct
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


def afis_uret(palet_no, tohum, baslik, tarih_metni, mekan, kategori):
    """Afisi uretir; Pillow yoksa yazisiz degrade doner."""
    zemin = _zemin(PALETLER[palet_no % len(PALETLER)], tohum)
    ham = _png(zemin)

    try:
        import io

        from PIL import Image, ImageDraw
    except ImportError:
        return ham

    gorsel = Image.open(io.BytesIO(ham)).convert("RGB")
    cizim = ImageDraw.Draw(gorsel)

    kenar = 48
    ic_genislik = GENISLIK - kenar * 2
    vurgu = PALETLER[palet_no % len(PALETLER)][2]

    # Kategori rozeti — ustte.
    kucuk = _yazi_tipi(YAZI_TIPLERI, 22)
    cizim.text((kenar, kenar), kategori.upper(), font=kucuk, fill=vurgu)

    # Baslik — alt bantta, asagidan yukari diziliyor ki uzun basliklar
    # yukari dogru bussun ve alt kenardan tasmasin.
    buyuk = _yazi_tipi(YAZI_TIPLERI, 46)
    satirlar = _sar(cizim, baslik, buyuk, ic_genislik)

    orta = _yazi_tipi(YAZI_TIPLERI_INCE, 26)

    y = YUKSEKLIK - kenar - 34
    cizim.text((kenar, y), mekan, font=orta, fill=(200, 200, 210))

    y -= 42
    cizim.text((kenar, y), tarih_metni, font=orta, fill=vurgu)

    for satir in reversed(satirlar):
        y -= 58
        cizim.text((kenar, y), satir, font=buyuk, fill=(255, 255, 255))

    cikti = io.BytesIO()
    gorsel.save(cikti, format="PNG", optimize=True)

    return cikti.getvalue()
