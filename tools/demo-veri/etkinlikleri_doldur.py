# -*- coding: utf-8 -*-
"""Demo katalogu: mevcut salonlara gercekci etkinlikler acar ve yayina alir.

Teslim oncesi temizlikte kabul betiklerinin urettigi 418 hesap silindi;
etkinliklerin TAMAMI o hesaplarin organizatorleriymis ve katalog bosaldi.
Bu betik katalogu demo hesaplariyla yeniden kuruyor.

Akisin tamamini GERCEK uclardan geciriyor: etkinlik olustur -> oturum ekle
→ bilet turu ekle -> afis yukle -> sahne sozlesmesi ekle -> onaya gonder →
yayina al. Veritabanina dogrudan yazilsaydi olusan kayitlar uygulamanin
kendi kurallarindan gecmemis olurdu; ornegin yayin sirasinda uretilen
satilabilir koltuklar hic olusmaz ve bilet alinamayan bir katalog
cikardi.

Betik TEKRARLANABILIR: ayni adla bir etkinlik varsa atliyor.

Kosum (API ayakta olmali):
    python tools/demo-veri/etkinlikleri_doldur.py
"""

import datetime as dt
import json
import os
import sys
import urllib.error
import urllib.request
import uuid

from afis_uret import afis_uret

# Hedef sunucu ve yonetici hesabi ortam degiskeninden okunuyor.
#
# Uc betikte de ayni satirlar elle degistiriliyordu; canliya cikarken uc
# dosyada alti satir duzenlemek hem sikici hem de birini unutmaya acik —
# unutulan betik yerel veritabanina yazar ve canliya bir sey gitmedigi
# ancak sonradan fark edilirdi.
#
#   Windows PowerShell:
#     $env:LOCA_API = "https://loca-xxxx.onrender.com/api/v1"
#     $env:LOCA_ADMIN_SIFRE = "..."
#
#   Bash:
#     export LOCA_API="https://loca-xxxx.onrender.com/api/v1"
#     export LOCA_ADMIN_SIFRE="..."
KOK = os.environ.get("LOCA_API", "http://localhost:5000/api/v1")
ADMIN = (
    os.environ.get("LOCA_ADMIN_EPOSTA", "admin@loca.dev"),
    os.environ.get("LOCA_ADMIN_SIFRE", "Loca!Admin2026"),
)
ORGANIZATOR = ("organizator@loca.dev", "Loca!Demo2026")

# Ad, kategori, aciklama, gun sonrasi, sure, bilet turleri (ad, fiyat).
#
# Gun sonrasi degerleri BILEREK dagitildi: 0 ve 1 olanlar "Bugün" ve
# "Yarın" suzgeclerinin bos donmemesi icin. Suzgec calisir ama gosterecek
# hicbir sey bulamazsa kullanici suzgecin bozuk oldugunu sanar.
ETKINLIKLER = [
    ("Yaz Akşamı Caz Konseri", "Konser",
     "Üçlü caz grubu; standartlar ve doğaçlama.", 0, 120,
     [("Tam", 450), ("Öğrenci", 250)]),
    ("Perşembe Akşamı Stand-up", "Stand-up",
     "Haftanın en yorgun gününe kısa bir mola.", 0, 70,
     [("Tam", 320), ("Öğrenci", 180)]),
    ("Akustik Oda Konseri", "Konser",
     "Elektriksiz düzenlemelerle bir saat.", 1, 90,
     [("Tam", 380), ("Öğrenci", 210)]),
    ("Çocuk Tiyatrosu: Kayıp Yıldız", "Çocuk",
     "5-10 yaş için müzikli oyun.", 1, 60,
     [("Tam", 180)]),
    ("Stand-up: Yeni Program", "Stand-up",
     "Yeni yazılmış bir saatlik gösteri.", 3, 75,
     [("Tam", 350), ("Öğrenci", 200)]),
    ("Bir Yaz Gecesi Rüyası", "Tiyatro",
     "Klasik komedinin modern sahnelemesi.", 4, 140,
     [("Tam", 400), ("Öğrenci", 220)]),
    ("Anadolu Rock Gecesi", "Konser",
     "Yerli rock repertuvarından seçmeler.", 5, 150,
     [("Tam", 600), ("Öğrenci", 320)]),
    ("Teknoloji ve Tasarım Zirvesi", "Konferans",
     "Ürün tasarımı ve yazılım üzerine tam günlük program.", 6, 300,
     [("Tam", 750), ("Öğrenci", 350)]),
    ("Fotoğraf Atölyesi", "Konferans",
     "Sokak fotoğrafçılığı üzerine uygulamalı çalışma.", 7, 180,
     [("Tam", 300)]),
    ("Klasik Müzik Dinletisi", "Konser",
     "Oda müziği topluluğundan bir akşam.", 8, 110,
     [("Tam", 520), ("Öğrenci", 280)]),
    ("Kısa Film Festivali", "Festival",
     "On beş kısa film, iki oturum ve yönetmen söyleşisi.", 9, 240,
     [("Tam", 280), ("Öğrenci", 150)]),
    ("Doğaçlama Tiyatro Gecesi", "Tiyatro",
     "Seyirciden gelen başlıklarla sahnede yazılan oyun.", 10, 100,
     [("Tam", 300), ("Öğrenci", 170)]),
    ("Caz Kvarteti: Gece Seansı", "Konser",
     "Geç saat programı; ikinci set doğaçlama.", 11, 120,
     [("Tam", 480), ("Öğrenci", 260)]),
    ("Çocuk Bilim Şenliği", "Çocuk",
     "Deney istasyonları ve gösteri; 7-12 yaş.", 12, 150,
     [("Tam", 200)]),
    ("Yazılım Mimarisi Semineri", "Konferans",
     "Katmanlı mimari ve eşzamanlılık üzerine yarım gün.", 13, 200,
     [("Tam", 650), ("Öğrenci", 300)]),
    ("Halk Müziği Gecesi", "Konser",
     "Yöresel repertuvar ve enstrüman tanıtımı.", 14, 130,
     [("Tam", 420), ("Öğrenci", 230)]),
    ("Tek Kişilik Oyun: Duvar", "Tiyatro",
     "Altmış dakikalık monolog.", 16, 60,
     [("Tam", 350), ("Öğrenci", 190)]),
    ("Elektronik Müzik Gecesi", "Festival",
     "Üç DJ, sabaha kadar program.", 18, 300,
     [("Tam", 700), ("Öğrenci", 400)]),
    ("Kukla Tiyatrosu: Denizin Sesi", "Çocuk",
     "3-8 yaş için kukla gösterisi.", 20, 50,
     [("Tam", 160)]),
    ("Yılın Son Stand-up'ı", "Stand-up",
     "Sezonu kapatan özel program.", 22, 90,
     [("Tam", 450), ("Öğrenci", 250)]),
]


def istek(yol, yontem="GET", govde=None, token=None, ek_basliklar=None):
    veri = json.dumps(govde).encode() if govde is not None else None
    basliklar = {"Content-Type": "application/json"} if veri else {}

    if token:
        basliklar["Authorization"] = f"Bearer {token}"

    basliklar.update(ek_basliklar or {})

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


def dosya_yukle(yol, ad, icerik, tur, token):
    """Cok parcali gonderim.

    urllib'in hazir bir multipart destegi yok; govde elle kuruluyor.
    Ek bir bagimlilik (requests) eklemek yerine bu tercih edildi —
    betik depoya giriyor ve tek dosyalik bir demo araci icin paket
    listesi buyutmenin karsiligi yok.
    """
    sinir = "----loca" + uuid.uuid4().hex

    govde = (
        f"--{sinir}\r\n"
        f'Content-Disposition: form-data; name="dosya"; filename="{ad}"\r\n'
        f"Content-Type: {tur}\r\n\r\n"
    ).encode() + icerik + f"\r\n--{sinir}--\r\n".encode()

    req = urllib.request.Request(
        KOK + yol,
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


def pdf_uret(baslik):
    """En kucuk gecerli PDF. Icerik onemli degil, imzasi (%PDF) onemli."""
    govde = (
        f"%PDF-1.4\n"
        f"1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n"
        f"2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n"
        f"3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]>>endobj\n"
        f"trailer<</Root 1 0 R>>\n"
        f"% {baslik}\n"
        f"%%EOF\n"
    )
    return govde.encode("latin-1", errors="replace")


def basarili(durum):
    return 200 <= durum < 300


def main():
    durum, cevap = istek("/auth/login", "POST",
                         {"email": ADMIN[0], "password": ADMIN[1]})
    if not basarili(durum):
        print(f"Admin girisi basarisiz: {durum} {cevap}")
        return 1
    admin = cevap["accessToken"]

    durum, cevap = istek("/auth/login", "POST",
                         {"email": ORGANIZATOR[0], "password": ORGANIZATOR[1]})
    if not basarili(durum):
        print(f"Organizator girisi basarisiz: {durum} {cevap}")
        return 1
    organizator = cevap["accessToken"]

    durum, kategoriler = istek("/event-categories")
    if not basarili(durum):
        print(f"Kategoriler alinamadi: {durum} {kategoriler}")
        return 1
    kategori_kimlik = {k["name"]: k["id"] for k in kategoriler}

    durum, mekanlar = istek("/venues?pageSize=200")
    if not basarili(durum):
        print(f"Mekanlar alinamadi: {durum} {mekanlar}")
        return 1

    # Mekan listesi sehir ADINI donuyor, kimligini degil; etkinlik ucu
    # kimlik istiyor. Ad-kimlik eslesmesi sehir listesinden kuruluyor.
    durum, sehirler = istek("/cities")
    if not basarili(durum):
        print(f"Sehirler alinamadi: {durum} {sehirler}")
        return 1
    sehir_kimlik = {s["name"]: s["id"] for s in sehirler}

    # Salonu ve oturma plani olan mekanlar; plansiz salonda oturum
    # acilamiyor.
    kullanilabilir = []

    for mekan in mekanlar["items"]:
        durum, salonlar = istek(f"/venues/{mekan['id']}/halls")
        if not basarili(durum):
            continue

        for salon in salonlar:
            durum, planlar = istek(f"/halls/{salon['id']}/seat-layouts")
            if basarili(durum) and planlar:
                kullanilabilir.append((mekan, salon, planlar[0]))

    if not kullanilabilir:
        print("Oturma plani olan salon yok; once mekanlari_doldur.py kosulmali.")
        return 1

    print(f"{len(kullanilabilir)} salon kullanilabilir.")

    durum, mevcut = istek("/events?pageSize=200", token=admin)
    mevcut_adlar = {e["title"] for e in (mevcut or {}).get("items", [])}

    simdi = dt.datetime.now(dt.timezone.utc).replace(microsecond=0)
    eklenen = 0

    for sira, (ad, kategori, aciklama, gun, sure, turler) in enumerate(ETKINLIKLER):
        if ad in mevcut_adlar:
            print(f"[atla] zaten var: {ad}")
            continue

        mekan, salon, plan = kullanilabilir[sira % len(kullanilabilir)]
        kategori_id = kategori_kimlik.get(kategori)

        if kategori_id is None:
            print(f"[atla] kategori yok: {kategori}")
            continue

        # Saat 20:00'a sabitleniyor: ayni salondaki iki etkinlik ayni
        # saate denk gelirse cakisma kontrolu ikincisini reddeder.
        # Sira numarasi saate ekleniyor ki ayni salona dusen etkinlikler
        # farkli gunlerde olsun.
        # Saat sira numarasina gore kayiyor: ayni gune ve ayni salona
        # dusen iki etkinlik ayni saatte olsaydi cakisma kontrolu ikincisini
        # reddederdi (araya bir saat temizlik payi da giriyor).
        baslangic = (simdi + dt.timedelta(days=gun)).replace(
            hour=17 + (sira % 3), minute=0, second=0)

        # SAAT SABITLENDIGI ICIN "bugun" GECMISE DUSEBILIYOR. gun=0 olan
        # etkinlik bugunun 17:00'sine yazilir; betik aksam ya da gece
        # kosulduysa etkinlik DOGDUGU ANDA gecmis olur. Satis bitisi de
        # baslangictan iki saat once oldugu icin katalog onu gosterir ama
        # rezervasyon 409 "Reservation.SalesEnded" doner — yani ekranda
        # duran, tiklanan, ama asla satin alinamayan bir etkinlik.
        # Gecmise dusen her etkinlik bir sonraki gune otelenip kurtariliyor.
        while baslangic <= simdi + dt.timedelta(hours=3):
            baslangic += dt.timedelta(days=1)

        bitis = baslangic + dt.timedelta(minutes=sure)

        satis_bas = simdi - dt.timedelta(days=1)
        satis_bit = baslangic - dt.timedelta(hours=2)

        durum, etkinlik_id = istek("/events", "POST", {
            "categoryId": kategori_id,
            "title": ad,
            "description": aciklama,
            "cancellationPolicy": "Etkinlik iptalinde bilet bedeli iade edilir.",
            "cityId": sehir_kimlik[mekan["cityName"]],
            "venueId": mekan["id"],
            "hallId": salon["id"],
            "eventDateUtc": baslangic.isoformat().replace("+00:00", "Z"),
            "durationMinutes": sure,
            "salesStartsAtUtc": satis_bas.isoformat().replace("+00:00", "Z"),
            "salesEndsAtUtc": satis_bit.isoformat().replace("+00:00", "Z"),
            "minimumAge": None,
        }, organizator)

        if not basarili(durum):
            print(f"[hata] etkinlik: {ad} -> {durum} {etkinlik_id}")
            continue

        durum, cevap = istek(f"/events/{etkinlik_id}/sessions", "POST", {
            "hallId": salon["id"],
            "seatLayoutId": plan["id"],
            "startsAtUtc": baslangic.isoformat().replace("+00:00", "Z"),
            "endsAtUtc": bitis.isoformat().replace("+00:00", "Z"),
            "salesStartsAtUtc": satis_bas.isoformat().replace("+00:00", "Z"),
            "salesEndsAtUtc": satis_bit.isoformat().replace("+00:00", "Z"),
        }, organizator)

        if not basarili(durum):
            print(f"[hata] oturum: {ad} -> {durum} {cevap}")
            continue

        # Kontenjan SALON KAPASITESINDEN hesaplaniyor, sabit yazilmiyor.
        # Sabit 200 yazildiginda kucuk salonlarda "toplam kontenjan
        # kapasiteyi asamaz" hatasi aliniyordu ve etkinlik bilet
        # turusuz kaliyordu — yani onaya bile gonderilemiyordu.
        kontenjan = max(1, salon["capacity"] // max(1, len(turler)))

        for tur_adi, fiyat in turler:
            durum, cevap = istek(f"/events/{etkinlik_id}/ticket-types", "POST", {
                "name": tur_adi,
                "price": fiyat,
                "currency": "TRY",
                "quota": kontenjan,
                "salesStartsAtUtc": satis_bas.isoformat().replace("+00:00", "Z"),
                "salesEndsAtUtc": satis_bit.isoformat().replace("+00:00", "Z"),
                # Ogrenci bileti dogrulama istiyor: sartnamedeki kural
                # demo veride de gorunmeli.
                "requiresVerification": tur_adi == "Öğrenci",
                "seatSectionId": None,
            }, organizator)

            if not basarili(durum):
                print(f"[hata] bilet turu: {ad}/{tur_adi} -> {durum} {cevap}")

        # Afis: uzerinde etkinligin adi, tarihi ve mekani yaziyor.
        # Once tek renk, sonra yazisiz degrade uretiliyordu; ikisi de
        # vitrinde bos bir renk blogu olarak gorunuyordu. Afis yayina
        # almanin domain on kosulu oldugu icin kaldirilamaz — dolu hale
        # getirildi.
        afis = afis_uret(
            palet_no=sira,
            tohum=sira,
            baslik=ad,
            tarih_metni=baslangic.strftime("%d.%m.%Y  %H:%M"),
            mekan=f"{mekan['name']} · {salon['name']}",
            kategori=kategori,
        )

        durum, dosya = dosya_yukle("/files", f"afis-{sira}.png",
                                   afis, "image/png", organizator)

        if basarili(durum):
            istek(f"/events/{etkinlik_id}/poster", "PATCH",
                  {"posterFileId": dosya["id"]}, organizator)
        else:
            print(f"[hata] afis: {ad} -> {durum} {dosya}")

        # Sahne sozlesmesi: onaya gondermenin on kosulu.
        durum, belge = dosya_yukle("/files/belge", f"sozlesme-{sira}.pdf",
                                   pdf_uret(ad), "application/pdf", organizator)

        if basarili(durum):
            istek(f"/events/{etkinlik_id}/documents", "POST", {
                "uploadedFileId": belge["id"],
                "kind": "VenueContract",
                "note": f"{mekan['name']} - {salon['name']}",
            }, organizator)
        else:
            print(f"[hata] belge: {ad} -> {durum} {belge}")

        durum, cevap = istek(f"/events/{etkinlik_id}/submit-for-approval",
                             "POST", None, organizator)
        if not basarili(durum):
            print(f"[hata] onaya gonderme: {ad} -> {durum} {cevap}")
            continue

        # Yayin ADMIN ile: satilabilir koltuklar burada uretiliyor.
        durum, cevap = istek(f"/events/{etkinlik_id}/publish", "POST", None, admin)
        if not basarili(durum):
            print(f"[hata] yayin: {ad} -> {durum} {cevap}")
            continue

        eklenen += 1
        print(f"[tamam] {ad} — {mekan['name']} / {salon['name']} "
              f"({baslangic.date()})")

    print(f"\n{eklenen} etkinlik yayina alindi.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
