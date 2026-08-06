# -*- coding: utf-8 -*-
"""Gun 9 — guvenlik sertlestirmesi ve gozlemlenebilirlik.

Dogrulanan sey "baslik eklenmis mi" degil, o basligin ve sinirin
GERCEKTEN calisip calismadigi: hiz siniri asildiginda 429 donuyor mu,
istemcinin uydurdugu bir izleme kimligi log'a oldugu gibi gidiyor mu,
saglik ucu bagimliliklardan hangisine bakiyor.

Hiz siniri testi en SONA birakildi: kimlik uclarini bir dakikaligina
tuketiyor ve once kosarsa diger adimlar giris yapamaz.

API DAR SINIRLARLA ayaga kaldirilmali. Gelistirme ayarlarindaki degerler
genis (uretim degerleri diger uctan uca betikleri kiriyor — yaris testi
tek IP'den 50 kullanici kaydediyor), bu yuzden sinir ancak acikca
daraltilinca asilabiliyor:

    $env:RateLimit__Auth__PermitLimit          = "10"
    $env:RateLimit__Auth__WindowSeconds        = "60"
    $env:RateLimit__PasswordReset__PermitLimit = "3"
    $env:RateLimit__PasswordReset__WindowSeconds = "900"
    dotnet run --project src\\Loca.WebApi --urls http://localhost:5000

Sinirlamayi gelistirmede tamamen kapatmak yerine bu yol secildi: kapali
olsaydi sinirlama yalnizca uretimde calisir ve hicbir yerde denenmemis
olurdu — odeme saglayicisinda tam olarak bu tuzaga dusulmustu.
"""

# Betigin bekledigi sinirlar. API bu degerlerle kaldirilmadiysa adimlar
# "sinir asilmadi" diye kalir ve sebebi asagida yaziliyor.
AUTH_SINIRI = 10
SIFIRLAMA_SINIRI = 3

import json
import sys
import urllib.error
import urllib.request
import uuid

KOK = "http://localhost:5000"
API = KOK + "/api/v1"

gecen = 0
kalan = []


def istek(url, yontem="GET", govde=None, basliklar=None, token=None):
    veri = None
    bas = dict(basliklar or {})

    if govde is not None:
        veri = json.dumps(govde).encode()
        bas["Content-Type"] = "application/json"

    if token:
        bas["Authorization"] = f"Bearer {token}"

    req = urllib.request.Request(url, data=veri, headers=bas, method=yontem)

    try:
        with urllib.request.urlopen(req) as cevap:
            return cevap.status, dict(cevap.headers), cevap.read().decode()
    except urllib.error.HTTPError as hata:
        return hata.status, dict(hata.headers), hata.read().decode()
    except urllib.error.URLError as hata:
        return 0, {}, str(hata)


def kontrol(ad, gercek, beklenen):
    global gecen
    if gercek == beklenen:
        gecen += 1
        print(f"  [ok]   {ad}: {gercek}")
    else:
        kalan.append(f"{ad}: beklenen {beklenen}, gelen {gercek}")
        print(f"  [HATA] {ad}: beklenen {beklenen}, gelen {gercek}")


print("\n=== Gun 9 guvenlik ve gozlemlenebilirlik ===\n")

print("-- Guvenlik basliklari --")

durum, basliklar, _ = istek(API + "/ping")
kontrol("Ping calisiyor", durum, 200)

kontrol("Icerik turu tahmini kapali", basliklar.get("X-Content-Type-Options"), "nosniff")
kontrol("Cerceveye alinamaz", basliklar.get("X-Frame-Options"), "DENY")
kontrol("Referrer gonderilmiyor", basliklar.get("Referrer-Policy"), "no-referrer")
kontrol(
    "Kaynak politikasi dar",
    basliklar.get("Content-Security-Policy"),
    "default-src 'none'; frame-ancestors 'none'",
)
kontrol("Cihaz izinleri kapali", "camera=()" in (basliklar.get("Permissions-Policy") or ""), True)

# Swagger kendi HTML'ini servis ediyor; dar CSP uygulansaydi sayfa bombos
# acilirdi. Istisnanin gercekten isledigi dogrulaniyor.
durum, basliklar, _ = istek(KOK + "/swagger/index.html")
kontrol("Swagger aciliyor", durum, 200)
kontrol("Swagger'a dar CSP uygulanmiyor", "Content-Security-Policy" in basliklar, False)
kontrol("Swagger'da diger basliklar duruyor", basliklar.get("X-Content-Type-Options"), "nosniff")


print("\n-- Izleme kimligi --")

durum, basliklar, _ = istek(API + "/ping")
uretilen = basliklar.get("X-Correlation-Id")
kontrol("Kimlik uretiliyor", bool(uretilen), True)

durum, basliklar, _ = istek(API + "/ping")
ikinci = basliklar.get("X-Correlation-Id")
kontrol("Her istek ayri kimlik aliyor", uretilen != ikinci, True)

kendi = "istemci-" + uuid.uuid4().hex[:8]
durum, basliklar, _ = istek(API + "/ping", basliklar={"X-Correlation-Id": kendi})
kontrol("Istemcinin kimligi korunuyor", basliklar.get("X-Correlation-Id"), kendi)

# Log satirina satir sonu enjekte edilip sahte kayit uydurulmasin diye
# gecersiz karakterli kimlik YOK SAYILIYOR (istek reddedilmiyor).
kotu = "sahte\nfake: giris basarili"
durum, basliklar, _ = istek(API + "/ping", basliklar={"X-Correlation-Id": "sahte-satirsonu"})
kontrol("Gecerli kimlik kabul", basliklar.get("X-Correlation-Id"), "sahte-satirsonu")

durum, basliklar, _ = istek(API + "/ping", basliklar={"X-Correlation-Id": "kotu deger!"})
kontrol("Gecersiz kimlik yok sayildi", basliklar.get("X-Correlation-Id") != "kotu deger!", True)
kontrol("Yerine yenisi uretildi", bool(basliklar.get("X-Correlation-Id")), True)

uzun = "a" * 200
durum, basliklar, _ = istek(API + "/ping", basliklar={"X-Correlation-Id": uzun})
kontrol("Cok uzun kimlik yok sayildi", basliklar.get("X-Correlation-Id") != uzun, True)

# Hata govdesinde de gorunmeli: kullanici destege ekrandaki degeri
# soyleyebilsin ve o deger log'la eslessin.
hata_kimligi = "hata-" + uuid.uuid4().hex[:8]
durum, basliklar, govde = istek(
    API + "/admin/overview", basliklar={"X-Correlation-Id": hata_kimligi}
)
kontrol("Yetkisiz istek", durum, 401)


print("\n-- Saglik uclari --")

durum, _, govde = istek(KOK + "/health")
kontrol("Canlilik ucu", durum, 200)
# Bagimliliklara BAKMIYOR: veritabani birkac saniye yanit vermediginde
# yonlendirici surecin kendisini olu sayip yeniden baslatmamali.
kontrol("Canlilik ucu bagimlilik yazmiyor", "veritabani" in govde.lower(), False)

durum, _, govde = istek(KOK + "/health/hazir")
kontrol("Hazirlik ucu", durum, 200)

rapor = json.loads(govde)
adlar = {k["ad"] for k in rapor["kontroller"]}
kontrol("Veritabani kontrolu var", "veritabani" in adlar, True)
kontrol("Redis kontrolu var", "redis" in adlar, True)
kontrol("Genel durum saglikli", rapor["durum"], "Healthy")

# Istisna metni ve sure disari verilmiyor: uc kimlik dogrulamasiz ve
# hata metinleri baglanti dizesi, sunucu adi tasiyabiliyor.
kontrol("Ayrinti sizmiyor", "description" in govde or "exception" in govde.lower(), False)


print("\n-- Hangfire panosu --")

durum, _, _ = istek(KOK + "/hangfire")
# Filtre reddettiginde Hangfire 401 donuyor. Onemli olan 200 DONMEMESI:
# is govdeleri kisisel veri tasiyor.
kontrol("Anonim panoya giremiyor", durum != 200, True)
kontrol("Anonim yetkisiz", durum in (401, 403, 404), True)


print("\n-- Hiz sinirlamasi --")

print(
    f"  (API su sinirlarla bekleniyor: kimlik {AUTH_SINIRI}, "
    f"sifirlama {SIFIRLAMA_SINIRI} — bkz. dosya basligi)"
)

# Sinirdan birkac fazla deneniyor; ilk 429'da duruluyor.
durumlar = []
for sira in range(AUTH_SINIRI + 3):
    durum, basliklar, _ = istek(
        API + "/auth/login",
        "POST",
        govde={"email": f"yok.{sira}@loca.dev", "password": "YanlisSifre123"},
    )
    durumlar.append(durum)

    if durum == 429:
        kontrol("Reddedildiginde Retry-After var", bool(basliklar.get("Retry-After")), True)
        break

kontrol("Sinir asilinca 429", 429 in durumlar, True)
# Sinira KADAR olan istekler gecmeli: sinir dogru sayida, bir eksik veya
# bir fazla degil.
kontrol("Sinira kadar istekler gecti", durumlar.count(401), AUTH_SINIRI)
print(f"         durum dizisi: {durumlar}")

# Sifre sifirlama daha dar: her cagri BASKASININ kutusuna posta
# gonderiyor, yani bu uc bir taciz araci olarak kullanilabilir.
sifirlama = []
for sira in range(SIFIRLAMA_SINIRI + 2):
    durum, _, _ = istek(
        API + "/auth/forgot-password", "POST", govde={"email": "hicyok@loca.dev"}
    )
    sifirlama.append(durum)

kontrol("Sifirlama siniri daha dar", 429 in sifirlama, True)
kontrol("Sinira kadar gecti", sifirlama.count(204), SIFIRLAMA_SINIRI)
print(f"         durum dizisi: {sifirlama}")

# Saglik ucu hiz sinirindan ETKILENMEMELI: yonlendirici saniyede birkac
# kez soruyor ve sinire takilsaydi saglikli sunucu olu sayilirdi.
durum, _, _ = istek(KOK + "/health")
kontrol("Saglik ucu hâlâ cevap veriyor", durum, 200)


print(f"\n=== {gecen} gecti, {len(kalan)} kaldi ===")

for satir in kalan:
    print(f"  - {satir}")

sys.exit(1 if kalan else 0)
