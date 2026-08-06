"""Loca — calisma anindaki ayarlar, SMTP ve sifre sifirlama postasi.

Denenen sey "uc cevap veriyor mu" degil su uc soru:

  1. Sir bir ayar (SMTP sifresi) okuma uclarindan SIZIYOR MU
  2. Panelden bos gonderilen sifre alani mevcut sifreyi SILIYOR MU
  3. Sifirlama postasi gercekten GIDIYOR MU ve token log'a mi dusuyor

Mailpit'in kendi API'si (8025) uzerinden postanin varligi ve icerigi
dogrulaniyor; "gonderildi" demek yetmiyor, karsi tarafta durmasi gerekiyor.

Kosmadan once: docker-compose up -d  +  API 5000'de ayakta olmali.
"""

import json
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid

KOK = "http://localhost:5000/api/v1"
MAILPIT = "http://localhost:8025/api/v1"

ADMIN = ("admin@loca.dev", "Loca!Admin2026")

gecen = 0
kalan = []


def istek(yol, yontem="GET", token=None, govde=None, kok=KOK):
    veri = None
    basliklar = {}

    if govde is not None:
        veri = json.dumps(govde).encode()
        basliklar["Content-Type"] = "application/json"

    if token:
        basliklar["Authorization"] = f"Bearer {token}"

    req = urllib.request.Request(kok + yol, data=veri, headers=basliklar, method=yontem)

    try:
        with urllib.request.urlopen(req) as cevap:
            icerik = cevap.read().decode()

            if not icerik:
                return cevap.status, None

            try:
                return cevap.status, json.loads(icerik)
            except json.JSONDecodeError:
                # Mailpit'in silme ucu duz metin donuyor; kendi API'miz
                # her zaman JSON ama bu yardimci ikisini de cagiriyor.
                return cevap.status, icerik
    except urllib.error.HTTPError as hata:
        icerik = hata.read().decode()
        try:
            return hata.status, json.loads(icerik) if icerik else None
        except json.JSONDecodeError:
            return hata.status, icerik
    except urllib.error.URLError as hata:
        return 0, str(hata)


def kontrol(baslik, gercek, beklenen):
    global gecen
    if gercek == beklenen:
        gecen += 1
        print(f"  [ok]   {baslik}: {gercek}")
    else:
        kalan.append(f"{baslik}: beklenen {beklenen}, gelen {gercek}")
        print(f"  [HATA] {baslik}: beklenen {beklenen}, gelen {gercek}")


def giris(eposta, sifre):
    durum, cevap = istek("/auth/login", "POST", govde={"email": eposta, "password": sifre})

    if durum != 200:
        print(f"Giris basarisiz ({eposta}): {durum} {cevap}")
        sys.exit(1)

    return cevap["accessToken"]


def mailpit_temizle():
    istek("/messages", "DELETE", kok=MAILPIT)


def mailpit_sonuncu():
    durum, cevap = istek("/messages", kok=MAILPIT)

    if durum != 200 or not cevap or not cevap.get("messages"):
        return None

    ozet = cevap["messages"][0]
    _, detay = istek(f"/message/{ozet['ID']}", kok=MAILPIT)

    return detay


def smtp_yaz(token, **alanlar):
    govde = {
        "host": "localhost",
        "port": 1025,
        "useSsl": False,
        "userName": "",
        "password": "",
        "fromAddress": "bilet@loca.dev",
        "fromName": "Loca",
        "clearPassword": False,
    }
    govde.update(alanlar)

    return istek("/admin/settings/smtp", "PUT", token=token, govde=govde)


print("\n=== Ayarlar ve SMTP kabul testi ===\n")

admin = giris(*ADMIN)

# Betik tekrar tekrar kosuluyor: onceki kosudan kalan sifre "sifre
# tanimsizken ne oluyor" adimini yalancı bir sekilde gecirirdi.
smtp_yaz(admin, clearPassword=True)

# Her kosuda yeni musteri: rol kontrolu icin sabit bir hesaba
# guvenilmiyor, o hesabin rolleri baska bir testte degismis olabilir.
musteri_eposta = f"ayar.{uuid.uuid4().hex[:8]}@loca.dev"
durum, _ = istek(
    "/auth/register",
    "POST",
    govde={
        "email": musteri_eposta,
        "password": "Loca!Test2026",
        "fullName": "Ayar Testi",
        "phoneNumber": None,
    },
)
kontrol("Musteri kaydi", durum, 200)
musteri = giris(musteri_eposta, "Loca!Test2026")


print("\n-- Yetki --")

durum, _ = istek("/admin/settings/smtp")
kontrol("Anonim SMTP ayarlarini goremiyor", durum, 401)

durum, _ = istek("/admin/settings/smtp", token=musteri)
kontrol("Musteri SMTP ayarlarini goremiyor", durum, 403)

durum, _ = istek("/admin/settings/smtp", "PUT", token=musteri, govde={
    "host": "kotu.example.com", "port": 25, "useSsl": False,
    "userName": None, "password": None,
    "fromAddress": "a@b.dev", "fromName": "X",
})
kontrol("Musteri SMTP ayarlarini yazamiyor", durum, 403)

durum, _ = istek("/admin/settings/smtp/test", "POST", token=musteri)
kontrol("Musteri baglanti denemesi yapamiyor", durum, 403)


print("\n-- Dogrulama --")

gecersizler = [
    ("Bos sunucu", {"host": "", "port": 1025, "fromAddress": "a@b.dev", "fromName": "Loca"}),
    ("Port 0", {"host": "localhost", "port": 0, "fromAddress": "a@b.dev", "fromName": "Loca"}),
    ("Port 70000", {"host": "localhost", "port": 70000, "fromAddress": "a@b.dev", "fromName": "Loca"}),
    ("Gecersiz gonderen", {"host": "localhost", "port": 1025, "fromAddress": "adres-degil", "fromName": "Loca"}),
    ("Bos gonderen adi", {"host": "localhost", "port": 1025, "fromAddress": "a@b.dev", "fromName": ""}),
]

for baslik, alanlar in gecersizler:
    govde = {"useSsl": False, "userName": None, "password": None, **alanlar}
    durum, cevap = istek("/admin/settings/smtp", "PUT", token=admin, govde=govde)
    kontrol(baslik, durum, 400)

    # Dogrulama hatasi ALAN BAZLI donmeli; "istek gecersiz" diyen tek
    # satirlik bir mesaj kullaniciya hangi alani duzeltecegini soylemiyor.
    if durum == 400 and isinstance(cevap, dict):
        kontrol(f"{baslik} alan bazli", bool(cevap.get("errors")), True)


print("\n-- Yazma ve okuma --")

durum, _ = smtp_yaz(admin)
kontrol("Mailpit ayarlari yazildi", durum, 204)

durum, ayarlar = istek("/admin/settings/smtp", token=admin)
kontrol("Ayarlar okundu", durum, 200)
kontrol("Sunucu", ayarlar["host"], "localhost")
kontrol("Port", ayarlar["port"], 1025)
kontrol("Gonderen", ayarlar["fromAddress"], "bilet@loca.dev")
kontrol("Kaynak veritabani", ayarlar["source"], "Database")
kontrol("Yapilandirilmis", ayarlar["isConfigured"], True)
kontrol("Sifre tanimsiz", ayarlar["hasPassword"], False)
# Sifre alani cevapta HIC OLMAMALI; bos donmesi bile "bir gun dolar" demek.
kontrol("Cevapta password alani yok", "password" in ayarlar, False)

durum, sonuc = istek("/admin/settings/smtp/test", "POST", token=admin)
kontrol("Baglanti denemesi", durum, 200)
kontrol("Baglanti basarili", sonuc["succeeded"], True)
kontrol("Hata mesaji yok", sonuc["error"], None)


print("\n-- Sifre saklama --")

durum, _ = smtp_yaz(admin, userName="loca", password="gizli-sifre-2026")
kontrol("Sifreli ayar yazildi", durum, 204)

durum, ayarlar = istek("/admin/settings/smtp", token=admin)
kontrol("Sifre tanimli gorunuyor", ayarlar["hasPassword"], True)
kontrol("Kullanici adi donuyor", ayarlar["userName"], "loca")
# Sifrenin kendisi cevabin HICBIR yerinde gecmemeli.
kontrol("Sifre cevapta hic gecmiyor", "gizli-sifre-2026" in json.dumps(ayarlar), False)

# ASIL SORU: baska bir alani duzeltirken sifre siliniyor mu.
# Panel sifreyi hic gostermedigi icin form her acildiginda o alan BOS
# geliyor; bos degeri yazsaydik yonetici gonderen adini degistirdiginde
# sifreyi farkinda olmadan siler ve postalar sessizce gitmemeye baslardi.
durum, _ = smtp_yaz(
    admin, userName="loca", fromAddress="destek@loca.dev", fromName="Loca Destek"
)
kontrol("Sifresiz guncelleme", durum, 204)

durum, ayarlar = istek("/admin/settings/smtp", token=admin)
kontrol("Gonderen degisti", ayarlar["fromAddress"], "destek@loca.dev")
kontrol("Sifre KORUNDU", ayarlar["hasPassword"], True)

# Celiskili istek: hem yeni sifre hem "kaldir".
durum, _ = smtp_yaz(admin, userName="loca", password="yeni", clearPassword=True)
kontrol("Sifre yazip ayni anda kaldirma reddediliyor", durum, 400)

durum, ayarlar = istek("/admin/settings/smtp", token=admin)
kontrol("Reddedilen istek sifreye dokunmadi", ayarlar["hasPassword"], True)

# Sifreyi kaldirmanin ACIK yolu. Bu olmasaydi bir kez kaydedilen sifre
# hicbir zaman silinemez, veritabaninda kalirdi.
durum, _ = smtp_yaz(admin, clearPassword=True)
kontrol("Sifre kaldirildi", durum, 204)

durum, ayarlar = istek("/admin/settings/smtp", token=admin)
kontrol("Sifre gercekten silindi", ayarlar["hasPassword"], False)

# Kullanici adi da temizlendi: Mailpit kimlik dogrulamasi istemiyor,
# girili kalirsa sonraki gonderim bosuna AUTH deniyor.
kontrol("Kullanici adi temizlendi", ayarlar["userName"] or "", "")


print("\n-- Sifre sifirlama postasi --")

mailpit_temizle()

durum, _ = istek("/auth/forgot-password", "POST", govde={"email": musteri_eposta})
kontrol("Sifirlama istegi", durum, 204)

posta = mailpit_sonuncu()
kontrol("Posta Mailpit'e dustu", posta is not None, True)

if posta:
    kontrol("Alici dogru", posta["To"][0]["Address"], musteri_eposta)
    kontrol("Gonderen dogru", posta["From"]["Address"], "bilet@loca.dev")
    # Turkce'de lower() "I"yi "ı" yapiyor; ASCII'ye indirirken hem "ş"
    # hem "ı" cevrilmezse arama tutmuyor.
    konu = posta["Subject"].lower().replace("ş", "s").replace("ı", "i")
    kontrol("Konu sifirlamadan bahsediyor", "sifirlama" in konu, True)

    html = posta.get("HTML") or ""
    metin = posta.get("Text") or ""

    kontrol("HTML govde var", len(html) > 0, True)
    # Duz metin karsiligi olmayan posta, HTML gostermeyen istemcilerde
    # bombos gorunur.
    kontrol("Duz metin karsiligi var", len(metin) > 0, True)

    eslesme = re.search(r"sifre-sifirla\?token=([^\"'&<\s]+)", html)
    kontrol("Sifirlama baglantisi var", eslesme is not None, True)

    if eslesme:
        token = urllib.parse.unquote(eslesme.group(1))

        # Postadaki token GERCEKTEN calismali: sifirlama akisi buradan
        # kopuk olsaydi posta gider ama kullanici sifresini degistiremezdi.
        durum, _ = istek(
            "/auth/reset-password",
            "POST",
            govde={"token": token, "newPassword": "Loca!Yeni2026"},
        )
        kontrol("Postadaki token ile sifirlama", durum, 204)

        durum, _ = istek(
            "/auth/login", "POST", govde={"email": musteri_eposta, "password": "Loca!Yeni2026"}
        )
        kontrol("Yeni sifreyle giris", durum, 200)

        durum, _ = istek(
            "/auth/login", "POST", govde={"email": musteri_eposta, "password": "Loca!Test2026"}
        )
        kontrol("Eski sifreyle giris kapali", durum, 401)

        # 401, 400 degil: "token yok", "suresi dolmus" ve "zaten
        # kullanilmis" bilerek ayni cevabi veriyor. Ayrilsaydi elindeki
        # degerin gecerli bir token olup olmadigi denenerek ogrenilirdi.
        durum, _ = istek(
            "/auth/reset-password",
            "POST",
            govde={"token": token, "newPassword": "Loca!Baska2026"},
        )
        kontrol("Ayni token ikinci kez calismiyor", durum, 401)

mailpit_temizle()

# Kayitsiz adres icin de 204: farkli cevap verilseydi bu uc bir
# "bu e-posta sistemde var mi" sorgulama araci olurdu.
durum, _ = istek("/auth/forgot-password", "POST", govde={"email": "hicyok@loca.dev"})
kontrol("Kayitsiz adres yine 204", durum, 204)
kontrol("Kayitsiz adrese posta gitmedi", mailpit_sonuncu() is None, True)


print("\n-- Yapilandirilmamis SMTP --")

# Sunucu adresi bos birakildiginda gonderim SESSIZCE BASARILI
# sayilmamali; yonetici SMTP'yi kurmadigini fark etmeli.
smtp_yaz(admin, host="olmayan-sunucu.invalid")

durum, sonuc = istek("/admin/settings/smtp/test", "POST", token=admin)
kontrol("Ulasilmayan sunucu denemesi 200 donuyor", durum, 200)
kontrol("Deneme basarisiz", sonuc["succeeded"], False)
kontrol("Sebep gosteriliyor", bool(sonuc["error"]), True)

# Ayarlari calisir hâle geri al.
smtp_yaz(admin)


print("\n-- Kullanici detayi --")

durum, liste = istek("/admin/users?pageNumber=1", token=admin)
kontrol("Kullanici listesi", durum, 200)

hedef = next((k for k in liste["items"] if k["email"] == musteri_eposta), None)
kontrol("Yeni musteri listede", hedef is not None, True)

if hedef:
    durum, detay = istek(f"/admin/users/{hedef['id']}", token=admin)
    kontrol("Detay admin'e aciliyor", durum, 200)
    kontrol("Detay dogru kullanici", detay["email"], musteri_eposta)

    ham = json.dumps(detay).lower()
    kontrol("Sifre ozeti donmuyor", "passwordhash" in ham, False)
    kontrol("Oturum belirteci donmuyor", "refreshtoken" in ham, False)
    kontrol("Bilet QR kodu donmuyor", "qrcode" in ham, False)

    durum, _ = istek(f"/admin/users/{hedef['id']}", token=musteri)
    kontrol("Musteri detay goremiyor", durum, 403)

durum, _ = istek(f"/admin/users/{uuid.uuid4()}", token=admin)
kontrol("Olmayan kullanici", durum, 404)


print(f"\n=== {gecen} gecti, {len(kalan)} kaldi ===")

for satir in kalan:
    print(f"  - {satir}")

sys.exit(1 if kalan else 0)
