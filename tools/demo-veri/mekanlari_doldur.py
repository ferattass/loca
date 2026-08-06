# -*- coding: utf-8 -*-
"""Sehirlere gercekci mekan, salon ve oturma plani ekler.

Veritabaninda yalnizca Ankara'da mekan vardi ve hepsi kabul
betiklerinin urettigi "Test Sahne <rastgele>" kayitlariydi; diger dort
sehir bombostu. Kesfet ekranindaki "Etrafinda neler oluyor" bolumu
mekana gore ozet cikardigi icin demo tek sehirden ibaret gorunuyordu.

Betik ISLEMI TEKRARLANABILIR: var olan mekani yeniden olusturmaya
calismiyor, adina bakip atliyor. Iki kez kosulmasi ikinci bir kopya
uretmiyor.

Salon kapasitesi uretilen koltuk sayisindan KUCUK OLAMAZ (sunucu 409
donuyor), bu yuzden kapasite plandan hesaplaniyor — elle yazilan bir
sayi, bolum eklendiginde sessizce yanlis hâle gelirdi.

Kosum:
    python tools/demo-veri/mekanlari_doldur.py
"""

import json
import sys
import urllib.error
import urllib.request

KOK = "http://localhost:5000/api/v1"
ADMIN = ("admin@loca.dev", "Loca!Admin2026")

# Her sehir icin mekanlar. Bolumler (ad, sira sayisi, sira basina koltuk).
SEHIRLER = {
    "İstanbul": [
        {
            "ad": "Loca Sahne Kadıköy",
            "adres": "Caferağa Mah. Moda Cad. No:12, Kadıköy",
            "aciklama": "Konser ve stand-up için akustiği güçlendirilmiş salon.",
            "salon": "Ana Salon",
            "bolumler": [("Orta", 12, 20), ("Balkon", 6, 18)],
        },
        {
            "ad": "Beyoğlu Sanat Merkezi",
            "adres": "Hüseyinağa Mah. İstiklal Cad. No:140, Beyoğlu",
            "aciklama": "Tiyatro ve söyleşi için klasik sahne düzeni.",
            "salon": "Büyük Sahne",
            "bolumler": [("Parter", 14, 22), ("Balkon", 5, 20)],
        },
    ],
    "Ankara": [
        {
            "ad": "Loca Sahne Çankaya",
            "adres": "Kızılay Mah. Atatürk Bulvarı No:88, Çankaya",
            "aciklama": "Çok amaçlı salon; konser ve konferans düzenine uygun.",
            "salon": "Ana Salon",
            "bolumler": [("Orta", 12, 20), ("Balkon", 6, 18)],
        },
        {
            "ad": "Kızılay Kültür Merkezi",
            "adres": "Bahçelievler Mah. 7. Cadde No:15, Çankaya",
            "aciklama": "Aile etkinlikleri ve çocuk oyunları için düzenlenmiş salon.",
            "salon": "Küçük Salon",
            "bolumler": [("Parter", 10, 16)],
        },
    ],
    "İzmir": [
        {
            "ad": "Alsancak Açıkhava Sahnesi",
            "adres": "Alsancak Mah. Kordon Boyu No:3, Konak",
            "aciklama": "Yaz aylarında açıkhava konserleri.",
            "salon": "Açıkhava",
            "bolumler": [("Sahne Önü", 10, 24), ("Kademe", 8, 24)],
        },
        {
            "ad": "Konak Sanat Sahnesi",
            "adres": "Konak Mah. Anafartalar Cad. No:57, Konak",
            "aciklama": "Tiyatro ve dinleti için orta ölçekli salon.",
            "salon": "Sahne A",
            "bolumler": [("Parter", 12, 18)],
        },
    ],
    "Bursa": [
        {
            "ad": "Nilüfer Kültür Merkezi",
            "adres": "Ertuğrul Mah. Nilüfer Cad. No:22, Nilüfer",
            "aciklama": "Konser, tiyatro ve panel salonu.",
            "salon": "Ana Salon",
            "bolumler": [("Orta", 12, 20), ("Balkon", 5, 18)],
        },
        {
            "ad": "Osmangazi Sahne",
            "adres": "Kayhan Mah. Cumhuriyet Cad. No:9, Osmangazi",
            "aciklama": "Stand-up ve söyleşi için kompakt sahne.",
            "salon": "Sahne",
            "bolumler": [("Parter", 9, 16)],
        },
    ],
    "Antalya": [
        {
            "ad": "Konyaaltı Sahil Sahnesi",
            "adres": "Liman Mah. Akdeniz Bulvarı No:4, Konyaaltı",
            "aciklama": "Deniz kenarında açıkhava sahnesi.",
            "salon": "Açıkhava",
            "bolumler": [("Sahne Önü", 10, 22), ("Kademe", 8, 22)],
        },
        {
            "ad": "Muratpaşa Kültür Salonu",
            "adres": "Meltem Mah. Dumlupınar Bulvarı No:31, Muratpaşa",
            "aciklama": "Festival ve çocuk etkinlikleri salonu.",
            "salon": "Ana Salon",
            "bolumler": [("Parter", 11, 18)],
        },
    ],
}

SIRA_ETIKETLERI = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"


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


def basarili(durum):
    """201 de basaridir.

    Ilk surumde yalnizca 200 kabul ediliyordu; olusturma uclari 201
    donuyor ve betik mekanlari GERCEKTEN olusturdugu hâlde hepsini hata
    diye raporladi. Kayitlar acilmis, salonlari acilmamisti.
    """
    return 200 <= durum < 300


def main():
    durum, cevap = istek(
        "/auth/login", "POST", {"email": ADMIN[0], "password": ADMIN[1]})

    if not basarili(durum):
        print(f"Admin girisi basarisiz: {durum} {cevap}")
        return 1

    token = cevap["accessToken"]

    durum, sehirler = istek("/cities")

    if not basarili(durum):
        print(f"Sehirler alinamadi: {durum} {sehirler}")
        return 1

    sehirKimlikleri = {s["name"]: s["id"] for s in sehirler}

    durum, mevcutlar = istek("/venues?pageSize=200")
    mevcutKimlikler = {m["name"]: m["id"] for m in (mevcutlar or {}).get("items", [])}

    eklenen = 0
    atlanan = 0

    for sehirAdi, mekanlar in SEHIRLER.items():
        sehirId = sehirKimlikleri.get(sehirAdi)

        if sehirId is None:
            print(f"[atla] sehir yok: {sehirAdi}")
            continue

        for mekan in mekanlar:
            # Var olan mekan ATLANMIYOR, salonundan devam ediliyor.
            # Ilk kosuda mekanlar acilmis ama salonlari acilmamisti
            # (201 hata sanildigi icin); tamamen atlansaydi o kayitlar
            # sonsuza kadar salonsuz kalirdi.
            mekanId = mevcutKimlikler.get(mekan["ad"])

            if mekanId is not None:
                durum, detay = istek(f"/venues/{mekanId}")

                if basarili(durum) and detay.get("halls"):
                    print(f"[var]  {sehirAdi:9} {mekan['ad']}")
                    atlanan += 1
                    continue

            # --- Kapasite plandan hesaplaniyor -------------------------
            # Elle yazilan bir sayi bolum eklendiginde sessizce yanlis
            # hâle gelirdi; sunucu kapasiteyi uretilen koltuk sayisinin
            # altina cekmeye izin vermiyor (409).
            toplamKoltuk = sum(sira * adet for _, sira, adet in mekan["bolumler"])

            if mekanId is None:
                durum, mekanId = istek("/venues", "POST", token=token, govde={
                    "cityId": sehirId,
                    "name": mekan["ad"],
                    "address": mekan["adres"],
                    "description": mekan["aciklama"],
                    "phoneNumber": None,
                })

                if not basarili(durum):
                    print(f"[HATA] mekan: {mekan['ad']} -> {durum} {mekanId}")
                    continue

            durum, salonId = istek(
                f"/venues/{mekanId}/halls", "POST", token=token,
                govde={"name": mekan["salon"], "capacity": toplamKoltuk})

            if not basarili(durum):
                print(f"[HATA] salon: {mekan['salon']} -> {durum} {salonId}")
                continue

            durum, planId = istek("/seat-layouts", "POST", token=token, govde={
                "hallId": salonId,
                "name": "Varsayılan plan",
                "description": f"{mekan['salon']} standart yerleşimi",
            })

            if not basarili(durum):
                print(f"[HATA] plan -> {durum} {planId}")
                continue

            # Bolumler ust uste binmesin diye her biri bir oncekinin
            # altindan basliyor.
            originY = 0

            for sira, (bolumAdi, siraSayisi, koltukSayisi) in enumerate(mekan["bolumler"]):
                durum, bolumId = istek(
                    f"/seat-layouts/{planId}/sections", "POST", token=token,
                    govde={"name": bolumAdi, "displayOrder": sira + 1})

                if not basarili(durum):
                    print(f"[HATA] bolum {bolumAdi} -> {durum} {bolumId}")
                    continue

                durum, sonuc = istek(
                    f"/seat-layouts/{planId}/generate-seats", "POST", token=token,
                    govde={
                        "seatSectionId": bolumId,
                        "rowLabels": list(SIRA_ETIKETLERI[:siraSayisi]),
                        "seatsPerRow": koltukSayisi,
                        "horizontalSpacing": 30,
                        "verticalSpacing": 35,
                        "originY": originY,
                    })

                if not basarili(durum):
                    print(f"[HATA] koltuk {bolumAdi} -> {durum} {sonuc}")
                    continue

                # Bir sonraki bolum icin bosluk: sira sayisi x aralik,
                # araya bir sira genisliginde ayirici.
                originY += siraSayisi * 35 + 70

            print(f"[yeni] {sehirAdi:9} {mekan['ad']:28} {toplamKoltuk:4} koltuk")
            eklenen += 1

    print(f"\n{eklenen} mekan eklendi, {atlanan} mekan zaten vardi.")

    return 0


if __name__ == "__main__":
    sys.exit(main())
