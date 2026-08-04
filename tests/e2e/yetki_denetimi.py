"""Loca — uc ve yetki denetimi.

Bugun eklenen her ucu, her rol icin ayri ayri deneyip beklenen durum
koduyla karsilastirir. Amac "calisiyor mu" degil, "yetkisi olmayan
gercekten giremiyor mu".
"""

import json
import uuid
import sys
import urllib.error
import urllib.request

KOK = "http://localhost:5000/api/v1"

HESAPLAR = {
    "admin": ("admin@loca.dev", "Loca!Admin2026"),
    "musteri": ("od.f0c2cbc8@loca.dev", "Loca!Test2026"),
    "organizator": ("org.f0c2cbc8@loca.dev", "Loca!Test2026"),
}

gecen = 0
kalan = []


def istek(yol, yontem="GET", token=None, govde=None, form=None):
    veri = None
    basliklar = {}

    if govde is not None:
        veri = json.dumps(govde).encode()
        basliklar["Content-Type"] = "application/json"
    elif form is not None:
        veri = form.encode()
        basliklar["Content-Type"] = "application/x-www-form-urlencoded"

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
    durum, govde = istek("/auth/login", "POST", govde={"email": eposta, "password": sifre})
    if durum != 200:
        print(f"  giris basarisiz ({eposta}): {durum} {govde}")
        return None
    return govde["accessToken"]


print("=== Oturumlar ===")
tokenlar = {}
for ad, (eposta, sifre) in HESAPLAR.items():
    tokenlar[ad] = giris(eposta, sifre)
    print(f"  {ad}: {'alindi' if tokenlar[ad] else 'ALINAMADI'}")

if not tokenlar["admin"]:
    print("Admin oturumu yok, denetim durduruldu.")
    sys.exit(1)

print()
print("=== 1. Yonetim uclari: yalnizca admin ===")
YONETIM_UCLARI = [
    ("/admin/overview", "GET"),
    ("/admin/payments", "GET"),
    ("/admin/users", "GET"),
    ("/admin/queue", "GET"),
]

for yol, yontem in YONETIM_UCLARI:
    durum, _ = istek(yol, yontem)
    kontrol(f"{yol} anonim", durum, 401)

    durum, _ = istek(yol, yontem, tokenlar["musteri"])
    kontrol(f"{yol} musteri", durum, 403)

    if tokenlar["organizator"]:
        durum, _ = istek(yol, yontem, tokenlar["organizator"])
        kontrol(f"{yol} organizator", durum, 403)

    durum, _ = istek(yol, yontem, tokenlar["admin"])
    kontrol(f"{yol} admin", durum, 200)

print()
print("=== 2. Bilet uclari ===")
durum, _ = istek("/tickets")
kontrol("/tickets anonim", durum, 401)

durum, biletler = istek("/tickets", token=tokenlar["musteri"])
kontrol("/tickets musteri", durum, 200)
print(f"         musterinin bilet sayisi: {len(biletler) if biletler else 0}")

# Baskasinin bileti: musterinin bir biletini organizatorle sorgula.
if biletler:
    baskasininBilet = biletler[0]["id"]
    durum, _ = istek(f"/tickets/{baskasininBilet}", token=tokenlar["organizator"])
    kontrol("baskasinin bileti (403 degil 404 olmali)", durum, 404)

    durum, _ = istek(f"/tickets/{baskasininBilet}", token=tokenlar["musteri"])
    kontrol("kendi bileti", durum, 200)

# Var olmayan bilet
durum, _ = istek("/tickets/00000000-0000-0000-0000-000000000000", token=tokenlar["musteri"])
kontrol("var olmayan bilet", durum, 404)

print()
print("=== 3. Kapida okutma ===")
durum, _ = istek("/tickets/check-in", "POST", govde={"qrCode": "deneme"})
kontrol("check-in anonim", durum, 401)

durum, _ = istek("/tickets/check-in", "POST", tokenlar["musteri"], {"qrCode": "SAHTEKOD"})
kontrol("check-in sahte kod (musteri)", durum, 404)

durum, _ = istek("/tickets/check-in", "POST", tokenlar["admin"], {"qrCode": ""})
kontrol("check-in bos kod", durum, 400)

durum, _ = istek("/tickets/check-in", "POST", tokenlar["admin"], {"qrCode": "x" * 100})
kontrol("check-in 100 karakter", durum, 400)

print()
print("=== 4. Odeme uclari ===")
durum, _ = istek("/payments/iyzico/callback", "POST", form="token=deneme")
kontrol("iyzico callback (Mock secili -> 404)", durum, 404)

durum, _ = istek("/payments/00000000-0000-0000-0000-000000000000/refund", "POST",
                 tokenlar["musteri"], {"reason": "deneme"})
kontrol("iade musteri (403)", durum, 403)

print()
print("=== 5. Rol degistirme kurallari ===")
durum, kullanicilar = istek("/admin/users?search=admin@loca.dev", token=tokenlar["admin"])
adminId = kullanicilar["items"][0]["id"] if durum == 200 and kullanicilar["items"] else None

if adminId:
    durum, _ = istek(f"/admin/users/{adminId}/roles", "POST", tokenlar["admin"],
                     {"roleName": "Admin", "grant": False})
    kontrol("kendi admin rolunu alma", durum, 409)

    durum, _ = istek(f"/admin/users/{adminId}/roles", "POST", tokenlar["admin"],
                     {"roleName": "Superuser", "grant": True})
    kontrol("gecersiz rol adi", durum, 400)

    durum, _ = istek(f"/admin/users/{adminId}/roles", "POST", tokenlar["musteri"],
                     {"roleName": "Admin", "grant": True})
    kontrol("musteri kendine admin vermeye calisiyor", durum, 403)

# Sifir GUID dogrulamaya takilir (400); var olmayan bir kullanici icin
# gecerli bicimde rastgele bir kimlik gerekiyor.
durum, _ = istek("/admin/users/00000000-0000-0000-0000-000000000000/roles", "POST",
                 tokenlar["admin"], {"roleName": "Customer", "grant": True})
kontrol("sifir GUID (dogrulama reddi)", durum, 400)

durum, _ = istek(f"/admin/users/{uuid.uuid4()}/roles", "POST",
                 tokenlar["admin"], {"roleName": "Customer", "grant": True})
kontrol("var olmayan kullaniciya rol", durum, 404)

print()
print("=== 6. Kuyruk uclari ===")
durum, _ = istek("/admin/queue?durum=Pending&limit=500", token=tokenlar["admin"])
kontrol("limit ust sinir", durum, 400)

durum, _ = istek("/admin/queue?durum=Pending&limit=0", token=tokenlar["admin"])
kontrol("limit alt sinir", durum, 400)

durum, mesajlar = istek("/admin/queue?durum=DeadLettered", token=tokenlar["admin"])
kontrol("olu mektup listesi", durum, 200)

durum, _ = istek("/admin/queue/00000000-0000-0000-0000-000000000000/requeue", "POST",
                 tokenlar["admin"])
kontrol("var olmayan mesaji geri koy", durum, 404)

# Islenmis bir mesaji geri koymaya calis: 409 donmeli
durum, islenmis = istek("/admin/queue?durum=Processed&limit=1", token=tokenlar["admin"])
if durum == 200 and islenmis:
    durum, _ = istek(f"/admin/queue/{islenmis[0]['id']}/requeue", "POST", tokenlar["admin"])
    kontrol("islenmis mesaji geri koy", durum, 409)

    # Kisisel veri sizintisi kontrolu
    alanlar = set(islenmis[0].keys())
    if "payload" in alanlar:
        kalan.append("KUYRUK GOVDESI DISARI VERILIYOR")
        print("  [HATA] kuyruk yaniti govdeyi tasiyor")
    else:
        gecen += 1
        print(f"  [ok]   kuyruk yaniti govde TASIMIYOR (alanlar: {sorted(alanlar)})")

print()
print("=== 7. Sayfalama sinirlari ===")
durum, sayfa = istek("/admin/payments?pageSize=1000", token=tokenlar["admin"])
if durum == 200:
    boyut = sayfa["pageSize"]
    if boyut <= 100:
        gecen += 1
        print(f"  [ok]   pageSize 1000 istendi, {boyut} verildi (ust sinir uygulandi)")
    else:
        kalan.append(f"pageSize siniri yok: {boyut}")
        print(f"  [HATA] pageSize siniri uygulanmadi: {boyut}")

durum, sayfa = istek("/admin/payments?pageNumber=-5", token=tokenlar["admin"])
if durum == 200:
    no = sayfa["pageNumber"]
    if no >= 1:
        gecen += 1
        print(f"  [ok]   pageNumber -5 istendi, {no} verildi")
    else:
        kalan.append(f"negatif sayfa numarasi kabul edildi: {no}")
        print(f"  [HATA] negatif sayfa numarasi: {no}")

print()
print("=" * 60)
print(f"Gecen: {gecen}   Kalan: {len(kalan)}")
for satir in kalan:
    print(f"  - {satir}")
sys.exit(1 if kalan else 0)
