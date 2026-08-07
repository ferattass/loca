# -*- coding: utf-8 -*-
"""Yayindaki her etkinlige okunabilir bir afis uretip baglar.

`etkinlikleri_doldur.py` yeni etkinliklere zaten afis takiyor; bu betik
MEVCUT etkinliklerin afislerini yeniliyor. Ucretsiz barindirma
katmanlarinda dosya sistemi gecici oldugu icin dagitimdan sonra da
kosulmasi gerekiyor: yuklenen dosyalar her dagitimda siliniyor.

Cizim `afis_uret.py`'de; Pillow kuruluysa afisin uzerine etkinligin adi,
tarihi ve mekani yaziliyor, degilse yazisiz degrade uretiliyor.

Kosum (API ayakta olmali):
    python tools/demo-veri/afisleri_yenile.py
"""

import datetime as dt
import json
import sys
import urllib.error
import urllib.request
import uuid

from afis_uret import afis_uret

KOK = "http://localhost:5000/api/v1"
ADMIN = ("admin@loca.dev", "Loca!Admin2026")


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
    except urllib.error.URLError as hata:
        print(f"API'ye ulasilamadi: {hata}")
        sys.exit(1)


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
        tarih = dt.datetime.fromisoformat(
            etkinlik["eventDateUtc"].replace("Z", "+00:00"))

        icerik = afis_uret(
            palet_no=sira,
            tohum=sira,
            baslik=etkinlik["title"],
            tarih_metni=tarih.strftime("%d.%m.%Y  %H:%M"),
            mekan=f"{etkinlik['venueName']} · {etkinlik['cityName']}",
            kategori=etkinlik["categoryName"],
        )

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
