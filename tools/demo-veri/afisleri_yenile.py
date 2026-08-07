# -*- coding: utf-8 -*-
"""Etkinliklere afis uretir ve bagliar.

Ilk demo verisinde afis olarak tek renk PNG kullanilmisti; kartlar
vitrinde duz bir renk blogu gosteriyordu. Bu betik her etkinlige kendi
renk paletinde dikey bir afis uretiyor: dikey degrade, capraz seritler ve
alt kenarda koyulasan bir perde (ustune yazi gelirse okunur kalsin diye).

<b>Cizim saf standart kutuphaneyle</b>: Pillow gibi bir bagimlilik
eklenmedi. Depoya giren tek dosyalik bir demo araci icin paket listesi
buyutmenin karsiligi yok ve bu betik CI'da da kosabilmeli.

Kosum (API ayakta olmali):
    python tools/demo-veri/afisleri_yenile.py
"""

import json
import struct
import sys
import urllib.error
import urllib.request
import uuid
import zlib

KOK = "http://localhost:5000/api/v1"
ADMIN = ("admin@loca.dev", "Loca!Admin2026")

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


def istek(yol, yontem="GET", govde=None, token=None):
    veri = json.dumps(govde).encode() if govde is not None else None
    basliklar = {"Content-Type": "application/json"} if veri else {}

    if token:
        basliklar["Authorization"] = f"Bearer {token}"

    req = urllib.request.Request(KOK + yol, data=veri, headers=basliklar, method=yontem)

    try:
        with urllib.request.urlopen(req) as cevap:
            icerik = cevap.read().decode()
            return cevap.status, json.loads(icerik) if icerik else None
    except urllib.error.HTTPError as hata:
        icerik = hata.read().decode()
        try:
            return hata.status, json.loads(icerik) if icerik else None
        except json.JSONDecodeError:
            return hata.status, icerik


def dosya_yukle(ad, icerik, token):
    sinir = "----loca" + uuid.uuid4().hex

    govde = (
        f"--{sinir}\r\n"
        f'Content-Disposition: form-data; name="dosya"; filename="{ad}"\r\n'
        f"Content-Type: image/png\r\n\r\n"
    ).encode() + icerik + f"\r\n--{sinir}--\r\n".encode()

    req = urllib.request.Request(
        KOK + "/files",
        data=govde,
        headers={
            "Content-Type": f"multipart/form-data; boundary={sinir}",
            "Authorization": f"Bearer {token}",
        },
        method="POST",
    )

    try:
        with urllib.request.urlopen(req) as cevap:
            return cevap.status, json.loads(cevap.read().decode())
    except urllib.error.HTTPError as hata:
        return hata.status, hata.read().decode()


def karistir(a, b, oran):
    """Iki rengi oranla karistirir. oran=0 → a, oran=1 → b."""
    return tuple(round(a[i] + (b[i] - a[i]) * oran) for i in range(3))


def afis_uret(palet, tohum):
    """Dikey degrade + capraz seritler + alt perde."""
    ust, alt, vurgu = palet
    satirlar = []

    for y in range(YUKSEKLIK):
        dikey = y / (YUKSEKLIK - 1)
        temel = karistir(ust, alt, dikey)

        piksel = bytearray()

        for x in range(GENISLIK):
            renk = temel

            # Capraz seritler: x + y sabit olan cizgiler. Tohum her
            # etkinlikte deseni kaydiriyor, sekiz afis birbirinin ayni
            # gorunmesin.
            konum = (x + y * 2 + tohum * 97) % 420

            if konum < 6:
                renk = karistir(temel, vurgu, 0.55)
            elif konum < 12:
                renk = karistir(temel, vurgu, 0.22)

            # Ust kosede yumusak bir isik: duz degrade fotograf gibi
            # durmuyordu.
            uzaklik = ((x - GENISLIK * 0.25) ** 2 + (y - YUKSEKLIK * 0.18) ** 2) ** 0.5
            if uzaklik < 320:
                renk = karistir(renk, vurgu, 0.18 * (1 - uzaklik / 320))

            # Alt perde: kart uzerinde yazi varsa okunur kalsin.
            if dikey > 0.62:
                renk = karistir(renk, (10, 10, 14), (dikey - 0.62) / 0.38 * 0.75)

            piksel += bytes(renk)

        # Her satir filtre baytiyla basliyor (0 = filtre yok).
        satirlar.append(b"\x00" + bytes(piksel))

    ham = b"".join(satirlar)

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


def main():
    durum, cevap = istek("/auth/login", "POST",
                         {"email": ADMIN[0], "password": ADMIN[1]})
    if durum != 200:
        print(f"Admin girisi basarisiz: {durum} {cevap}")
        return 1
    token = cevap["accessToken"]

    durum, liste = istek("/events?pageSize=100", token=token)
    if durum != 200:
        print(f"Etkinlikler alinamadi: {durum} {liste}")
        return 1

    yenilenen = 0

    for sira, etkinlik in enumerate(liste["items"]):
        icerik = afis_uret(PALETLER[sira % len(PALETLER)], sira)

        durum, dosya = dosya_yukle(f"afis-{sira}.png", icerik, token)

        if durum != 200:
            print(f"[hata] yukleme: {etkinlik['title']} -> {durum} {dosya}")
            continue

        durum, cevap = istek(f"/events/{etkinlik['id']}/poster", "PATCH",
                             {"posterFileId": dosya["id"]}, token)

        if durum >= 300:
            print(f"[hata] baglama: {etkinlik['title']} -> {durum} {cevap}")
            continue

        yenilenen += 1
        print(f"[tamam] {etkinlik['title']} ({len(icerik) // 1024} KB)")

    print(f"\n{yenilenen} afis yenilendi.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
