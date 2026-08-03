# -*- coding: utf-8 -*-
"""Gun 6 uctan uca dogrulama: koltuk kilitleme ve rezervasyon.

Gercek istek, gercek veritabani, gercek Redis. API'nin
Reservation__LockMinutes=1 ile calistigi varsayiliyor (sure dolumu testi
icin); kilit suresi zaten bu yuzden yapilandirmadan okunuyor.

Kullanim:
    python e2e_gun6.py            tam senaryo
    python e2e_gun6.py --hizli    sure dolumu beklemesini atla
"""

import json
import sys
import time
import uuid
from concurrent.futures import ThreadPoolExecutor

import requests

BASE = "http://localhost:5000/api/v1"
ADMIN = ("admin@loca.dev", "Loca!Admin2026")
SIFRE = "Loca!Test2026"

HIZLI = "--hizli" in sys.argv

gecen = 0
kalan = 0
hatalar = []


def kontrol(ad, kosul, ayrinti=""):
    global gecen, kalan
    if kosul:
        gecen += 1
        print(f"  [OK]   {ad}")
    else:
        kalan += 1
        hatalar.append(f"{ad} :: {ayrinti}")
        print(f"  [HATA] {ad}  -> {ayrinti}")


def baslik(metin):
    print(f"\n=== {metin} ===")


def cagir(method, yol, token=None, govde=None, basliklar=None, files=None):
    h = dict(basliklar or {})
    if token:
        h["Authorization"] = f"Bearer {token}"
    return requests.request(
        method, f"{BASE}{yol}", json=govde, headers=h, files=files, timeout=30
    )


def kod(cevap):
    """Problem Details icindeki makine okunabilir hata kodu."""
    try:
        return cevap.json().get("code")
    except Exception:
        return None


def basliklar_401(cevap):
    return cevap.status_code == 401


def giris(eposta, sifre):
    c = cagir("POST", "/auth/login", govde={"email": eposta, "password": sifre})
    c.raise_for_status()
    return c.json()["accessToken"]


def kayit(eposta, ad):
    c = cagir(
        "POST",
        "/auth/register",
        govde={
            "email": eposta,
            "password": SIFRE,
            "fullName": ad,
            "phoneNumber": None,
        },
    )
    c.raise_for_status()
    return c.json()["accessToken"]


# --- Minimal gecerli PNG (1x1) -------------------------------------------
PNG = bytes.fromhex(
    "89504e470d0a1a0a0000000d49484452000000010000000108060000001f15c4"
    "890000000a49444154789c6360000002000100ffff03000006000557bfabd400"
    "00000049454e44ae426082"
)


def kurulum():
    """Sehir - mekan - salon - plan - etkinlik zinciri ve yayin."""
    ek = uuid.uuid4().hex[:8]
    veri = {}

    admin = giris(*ADMIN)
    veri["admin"] = admin

    # --- Organizator: basvuru + admin onayi -------------------------------
    org_eposta = f"org.{ek}@loca.dev"
    kayit(org_eposta, "Organizator Test")
    org = giris(org_eposta, SIFRE)

    c = cagir(
        "POST",
        "/organizer-applications",
        token=org,
        govde={
            "companyName": f"Test Prodüksiyon {ek}",
            "contactEmail": org_eposta,
            "contactPhone": "05001112233",
            "taxNumber": None,
            "website": None,
            "documentFileId": None,
        },
    )
    c.raise_for_status()
    basvuru_id = c.json() if isinstance(c.json(), str) else c.json().get("id", c.json())

    c = cagir(
        "POST",
        f"/organizer-applications/{basvuru_id}/review",
        token=admin,
        govde={"approve": True, "rejectionReason": None},
    )
    c.raise_for_status()

    # Rol token'a yeni girdi: yeniden giris gerekiyor.
    org = giris(org_eposta, SIFRE)
    veri["organizator"] = org
    veri["org_eposta"] = org_eposta

    # --- Sehir, mekan, salon ---------------------------------------------
    sehirler = cagir("GET", "/cities").json()
    sehir_id = sehirler[0]["id"]

    c = cagir(
        "POST",
        "/venues",
        token=admin,
        govde={
            "cityId": sehir_id,
            "name": f"Test Sahne {ek}",
            "address": "Test Mah. 1",
            "description": None,
            "phoneNumber": None,
        },
    )
    c.raise_for_status()
    mekan_id = c.json()

    c = cagir(
        "POST",
        f"/venues/{mekan_id}/halls",
        token=admin,
        govde={"name": f"Buyuk Salon {ek}", "capacity": 300},
    )
    c.raise_for_status()
    salon_id = c.json()

    c = cagir(
        "POST",
        "/seat-layouts",
        token=admin,
        govde={"hallId": salon_id, "name": f"Standart Plan {ek}", "description": None},
    )
    c.raise_for_status()
    plan_id = c.json()

    bolumler = {}
    for ad, sira in (("Orta", 1), ("Balkon", 2), ("Ogrenci", 3)):
        c = cagir(
            "POST",
            f"/seat-layouts/{plan_id}/sections",
            token=admin,
            govde={"name": ad, "displayOrder": sira},
        )
        c.raise_for_status()
        bolumler[ad] = c.json()

    # Orta 8x20 = 160, Balkon 4x10 = 40, Ogrenci 2x10 = 20  -> 220 koltuk
    for ad, satirlar, koltuk, y in (
        ("Orta", ["A", "B", "C", "D", "E", "F", "G", "H"], 20, 0),
        ("Balkon", ["J", "K", "L", "M"], 10, 400),
        ("Ogrenci", ["N", "P"], 10, 600),
    ):
        c = cagir(
            "POST",
            f"/seat-layouts/{plan_id}/generate-seats",
            token=admin,
            govde={
                "seatSectionId": bolumler[ad],
                "rowLabels": satirlar,
                "seatsPerRow": koltuk,
                "horizontalSpacing": 30,
                "verticalSpacing": 35,
                "originY": y,
            },
        )
        c.raise_for_status()

    # --- Etkinlik ---------------------------------------------------------
    kategoriler = cagir("GET", "/event-categories").json()
    kategori_id = kategoriler[0]["id"]

    etkinlik_tarihi = "2026-12-01T19:00:00Z"

    c = cagir(
        "POST",
        "/events",
        token=org,
        govde={
            "categoryId": kategori_id,
            "title": f"Yaris Testi Gecesi {ek}",
            "description": "Gun 6 kabul testi icin olusturuldu.",
            "cancellationPolicy": "Etkinlikten 24 saat oncesine kadar tam iade.",
            "cityId": sehir_id,
            "venueId": mekan_id,
            "hallId": salon_id,
            "eventDateUtc": etkinlik_tarihi,
            "durationMinutes": 120,
            "salesStartsAtUtc": "2026-01-01T00:00:00Z",
            "salesEndsAtUtc": "2026-11-30T00:00:00Z",
            "minimumAge": None,
        },
    )
    c.raise_for_status()
    etkinlik_id = c.json()

    # Satisi ACIK oturum
    c = cagir(
        "POST",
        f"/events/{etkinlik_id}/sessions",
        token=org,
        govde={
            "hallId": salon_id,
            "seatLayoutId": plan_id,
            "startsAtUtc": "2026-12-01T19:00:00Z",
            "endsAtUtc": "2026-12-01T21:00:00Z",
            "salesStartsAtUtc": "2026-01-01T00:00:00Z",
            "salesEndsAtUtc": "2026-11-30T00:00:00Z",
        },
    )
    c.raise_for_status()
    oturum_id = c.json()

    # Satisi HENUZ BASLAMAMIS ikinci oturum (temizlik payiyla ayri gunde)
    c = cagir(
        "POST",
        f"/events/{etkinlik_id}/sessions",
        token=org,
        govde={
            "hallId": salon_id,
            "seatLayoutId": plan_id,
            "startsAtUtc": "2026-12-05T19:00:00Z",
            "endsAtUtc": "2026-12-05T21:00:00Z",
            "salesStartsAtUtc": "2026-11-01T00:00:00Z",
            "salesEndsAtUtc": "2026-12-04T00:00:00Z",
        },
    )
    c.raise_for_status()
    ileri_oturum_id = c.json()

    # --- Bilet turleri ----------------------------------------------------
    def bilet_turu(ad, fiyat, kontenjan, bolum, belge=False):
        c = cagir(
            "POST",
            f"/events/{etkinlik_id}/ticket-types",
            token=org,
            govde={
                "name": ad,
                "price": fiyat,
                "currency": "TRY",
                "quota": kontenjan,
                "salesStartsAtUtc": "2026-01-01T00:00:00Z",
                "salesEndsAtUtc": "2026-11-30T00:00:00Z",
                "requiresVerification": belge,
                "seatSectionId": bolum,
            },
        )
        c.raise_for_status()
        return c.json()

    bilet_turu("Tam", 450, 160, bolumler["Orta"])
    bilet_turu("Balkon", 200, 40, bolumler["Balkon"])
    bilet_turu("Ogrenci", 120, 20, bolumler["Ogrenci"], belge=True)

    # --- Afis + onay + yayin ---------------------------------------------
    c = requests.post(
        f"{BASE}/files",
        headers={"Authorization": f"Bearer {org}"},
        files={"dosya": ("afis.png", PNG, "image/png")},
        timeout=30,
    )
    c.raise_for_status()
    dosya = c.json()
    dosya_id = dosya["id"] if isinstance(dosya, dict) else dosya

    c = cagir(
        "PATCH", f"/events/{etkinlik_id}/poster", token=org, govde={"posterFileId": dosya_id}
    )
    c.raise_for_status()

    c = cagir("POST", f"/events/{etkinlik_id}/submit-for-approval", token=org)
    c.raise_for_status()

    c = cagir("POST", f"/events/{etkinlik_id}/publish", token=admin)
    c.raise_for_status()
    yayin = c.json()

    veri.update(
        {
            "ek": ek,
            "etkinlik_id": etkinlik_id,
            "oturum_id": oturum_id,
            "ileri_oturum_id": ileri_oturum_id,
            "bolumler": bolumler,
            "uretilen_koltuk": yayin["generatedSeatCount"],
        }
    )
    return veri


def koltuklar(oturum_id, token=None, bolum_adi=None):
    c = cagir("GET", f"/event-sessions/{oturum_id}/seat-availability", token=token)
    c.raise_for_status()
    veri = c.json()
    cikti = []
    for bolum in veri["sections"]:
        if bolum_adi and bolum["name"] != bolum_adi:
            continue
        cikti.extend(bolum["seats"])
    return cikti


def musait(oturum_id, bolum_adi, adet, token=None):
    hepsi = [k for k in koltuklar(oturum_id, token, bolum_adi) if k["status"] == "Available"]
    return hepsi[:adet]


def rezerve_et(token, oturum_id, koltuk_idleri, anahtar=None):
    return cagir(
        "POST",
        "/reservations",
        token=token,
        govde={"eventSessionId": oturum_id, "eventSeatIds": koltuk_idleri},
        basliklar={"Idempotency-Key": anahtar or str(uuid.uuid4())},
    )


def main():
    print("Gun 6 — koltuk kilitleme ve rezervasyon, uctan uca dogrulama")
    print("=" * 62)

    baslik("Kurulum")
    v = kurulum()
    kontrol(
        "Etkinlik yayina alindi ve koltuklar uretildi",
        v["uretilen_koltuk"] == 440,
        f"uretilen={v['uretilen_koltuk']} (2 oturum x 220 bekleniyor)",
    )

    oturum = v["oturum_id"]

    a_eposta = f"a.{v['ek']}@loca.dev"
    b_eposta = f"b.{v['ek']}@loca.dev"
    kayit(a_eposta, "Musteri A")
    kayit(b_eposta, "Musteri B")
    a = giris(a_eposta, SIFRE)
    b = giris(b_eposta, SIFRE)

    # ------------------------------------------------------------------
    baslik("1 · Yetkilendirme ve temel akis")

    secim = musait(oturum, "Orta", 2)
    idler = [k["eventSeatId"] for k in secim]

    c = rezerve_et(None, oturum, idler)
    kontrol("Anonim rezervasyon 401", c.status_code == 401, c.status_code)

    c = rezerve_et(a, oturum, idler)
    kontrol("Gecerli rezervasyon 200", c.status_code == 200, f"{c.status_code} {c.text[:180]}")
    rez = c.json()
    rez_id = rez["id"]

    kontrol(
        "Tutar sunucuda hesaplandi (2 x 450 = 900)",
        rez["totalAmount"] == 900 and rez["currency"] == "TRY",
        f"{rez['totalAmount']} {rez['currency']}",
    )
    kontrol("Durum Pending", rez["status"] == "Pending", rez["status"])
    kontrol(
        "Kalan sure sunucudan geliyor",
        0 < rez["remainingSeconds"] <= 60,
        rez["remainingSeconds"],
    )
    kontrol("Koltuk sayisi 2", len(rez["seats"]) == 2, len(rez["seats"]))
    kontrol(
        "Koltuk etiketi tasiniyor (A-1 gibi)",
        all(s["rowLabel"] and s["seatNumber"] for s in rez["seats"]),
        rez["seats"][0],
    )

    # ------------------------------------------------------------------
    baslik("2 · Koltuk plani kilidi gosteriyor")

    a_gorunum = {k["eventSeatId"]: k for k in koltuklar(oturum, a, "Orta")}
    b_gorunum = {k["eventSeatId"]: k for k in koltuklar(oturum, b, "Orta")}

    kontrol(
        "Koltuklar Locked",
        all(a_gorunum[i]["status"] == "Locked" for i in idler),
        [a_gorunum[i]["status"] for i in idler],
    )
    kontrol(
        "Sahibine isLockedByMe true",
        all(a_gorunum[i]["isLockedByMe"] for i in idler),
        "A icin",
    )
    kontrol(
        "Baskasina isLockedByMe false",
        all(not b_gorunum[i]["isLockedByMe"] for i in idler),
        "B icin",
    )
    kontrol(
        "lockedByUserId disari sizmiyor",
        all("lockedByUserId" not in a_gorunum[i] for i in idler),
        list(a_gorunum[idler[0]].keys()),
    )

    # ------------------------------------------------------------------
    baslik("3 · Cakisma ve idempotency")

    c = rezerve_et(b, oturum, idler)
    kontrol(
        "Baskasinin kilitli koltugu 409 + SeatNotAvailable",
        c.status_code == 409 and kod(c) == "Reservation.SeatNotAvailable",
        f"{c.status_code} {kod(c)}",
    )

    anahtar = str(uuid.uuid4())
    yeni = musait(oturum, "Orta", 1)
    c1 = rezerve_et(a, oturum, [yeni[0]["eventSeatId"]], anahtar)
    c2 = rezerve_et(a, oturum, [yeni[0]["eventSeatId"]], anahtar)
    kontrol(
        "Ayni Idempotency-Key ikinci istek ayni kaydi donuyor",
        c1.status_code == 200
        and c2.status_code == 200
        and c1.json()["id"] == c2.json()["id"],
        f"{c1.status_code}/{c2.status_code}",
    )
    idempotent_rez = c1.json()["id"]

    c = rezerve_et(a, oturum, [yeni[0]["eventSeatId"]])
    kontrol(
        "Farkli anahtar, ayni koltuk 409",
        c.status_code == 409 and kod(c) == "Reservation.SeatNotAvailable",
        f"{c.status_code} {kod(c)}",
    )

    c = cagir("POST", "/reservations", token=a, govde={
        "eventSessionId": oturum, "eventSeatIds": [yeni[0]["eventSeatId"]]})
    kontrol("Idempotency-Key basligi yoksa 400", c.status_code == 400, c.status_code)

    # ------------------------------------------------------------------
    baslik("4 · Sahiplik ve listeleme")

    c = cagir("GET", f"/reservations/{rez_id}", token=a)
    kontrol("Sahibi detayi goruyor 200", c.status_code == 200, c.status_code)

    c = cagir("GET", f"/reservations/{rez_id}", token=b)
    kontrol(
        "Baskasinin rezervasyonu 403 (404 degil)",
        c.status_code == 403 and kod(c) == "Reservation.NotOwner",
        f"{c.status_code} {kod(c)}",
    )

    c = cagir("GET", f"/reservations/{rez_id}", token=v["admin"])
    kontrol("Admin her rezervasyonu goruyor", c.status_code == 200, c.status_code)

    c = cagir("GET", "/users/me/reservations", token=a)
    kontrol(
        "Rezervasyonlarim listesi",
        c.status_code == 200 and len(c.json()) == 2,
        f"{c.status_code} adet={len(c.json()) if c.status_code == 200 else '-'}",
    )

    # ------------------------------------------------------------------
    baslik("5 · Uzatma")

    onceki = cagir("GET", f"/reservations/{rez_id}", token=a).json()["expiresAtUtc"]
    c = cagir("POST", f"/reservations/{rez_id}/extend", token=a)
    kontrol(
        "Uzatma 200 ve bitis oteledi",
        c.status_code == 200 and c.json()["expiresAtUtc"] > onceki,
        f"{c.status_code} {onceki} -> {c.json().get('expiresAtUtc') if c.status_code == 200 else '-'}",
    )
    kontrol(
        "extensionUsed true",
        c.status_code == 200 and c.json()["extensionUsed"],
        c.text[:120],
    )

    c = cagir("POST", f"/reservations/{rez_id}/extend", token=a)
    kontrol("Ikinci uzatma 409", c.status_code == 409, f"{c.status_code} {c.text[:120]}")

    c = cagir("POST", f"/reservations/{rez_id}/extend", token=b)
    kontrol("Baskasinin rezervasyonunu uzatma 403", c.status_code == 403, c.status_code)

    # ------------------------------------------------------------------
    baslik("6 · Iptal koltugu hemen serbest birakiyor")

    c = cagir("POST", f"/reservations/{idempotent_rez}/cancel", token=a)
    kontrol("Iptal 204", c.status_code == 204, f"{c.status_code} {c.text[:120]}")

    serbest = {k["eventSeatId"]: k for k in koltuklar(oturum, a, "Orta")}
    kontrol(
        "Iptal edilen koltuk Available",
        serbest[yeni[0]["eventSeatId"]]["status"] == "Available",
        serbest[yeni[0]["eventSeatId"]]["status"],
    )

    c = cagir("POST", f"/reservations/{idempotent_rez}/cancel", token=a)
    kontrol("Ikinci iptal 409", c.status_code == 409, c.status_code)

    c = rezerve_et(b, oturum, [yeni[0]["eventSeatId"]])
    kontrol(
        "Serbest kalan koltugu baskasi alabiliyor",
        c.status_code == 200,
        f"{c.status_code} {c.text[:160]}",
    )
    b_rez = c.json()["id"]

    # ------------------------------------------------------------------
    baslik("7 · Bilet limiti (oturum basina 6)")

    fazla = [k["eventSeatId"] for k in musait(oturum, "Orta", 7)]
    c = rezerve_et(b, oturum, fazla)
    kontrol(
        "Tek istekte 7 koltuk reddediliyor",
        c.status_code in (400, 409),
        f"{c.status_code} {kod(c)}",
    )

    # B'nin 1 bileti var; 5 daha alinca 6 olur, 6. istek limiti asar.
    bes = [k["eventSeatId"] for k in musait(oturum, "Orta", 5)]
    c = rezerve_et(b, oturum, bes)
    kontrol("B toplam 6 bilete ulasti", c.status_code == 200, f"{c.status_code} {c.text[:160]}")

    bir_daha = [k["eventSeatId"] for k in musait(oturum, "Orta", 1)]
    c = rezerve_et(b, oturum, bir_daha)
    kontrol(
        "Limiti asan istek 409 + SeatLimitExceeded",
        c.status_code == 409 and kod(c) == "Reservation.SeatLimitExceeded",
        f"{c.status_code} {kod(c)}",
    )

    # ------------------------------------------------------------------
    baslik("8 · Oturum ve koltuk dogrulamalari")

    baska_oturum_koltugu = musait(v["ileri_oturum_id"], "Orta", 1)
    c = rezerve_et(a, oturum, [baska_oturum_koltugu[0]["eventSeatId"]])
    kontrol(
        "Baska oturumun koltugu 404 + SeatNotInSession",
        c.status_code == 404 and kod(c) == "Reservation.SeatNotInSession",
        f"{c.status_code} {kod(c)}",
    )

    c = rezerve_et(a, v["ileri_oturum_id"], [baska_oturum_koltugu[0]["eventSeatId"]])
    kontrol(
        "Satisi baslamamis oturum 409 + SalesNotStarted",
        c.status_code == 409 and kod(c) == "Reservation.SalesNotStarted",
        f"{c.status_code} {kod(c)}",
    )

    c = rezerve_et(a, oturum, [str(uuid.uuid4())])
    kontrol(
        "Var olmayan koltuk 404",
        c.status_code == 404,
        f"{c.status_code} {kod(c)}",
    )

    c = rezerve_et(a, str(uuid.uuid4()), [idler[0]])
    kontrol(
        "Var olmayan oturum 404 + SessionNotFound",
        c.status_code == 404 and kod(c) == "Reservation.SessionNotFound",
        f"{c.status_code} {kod(c)}",
    )

    # ------------------------------------------------------------------
    baslik("9 · Ogrenci dogrulamasi satin almaya bagli")

    ogr_eposta = f"ogr.{v['ek']}@loca.dev"
    kayit(ogr_eposta, "Ogrenci Test")
    ogr = giris(ogr_eposta, SIFRE)

    ogr_koltuk = musait(oturum, "Ogrenci", 1)
    c = rezerve_et(ogr, oturum, [ogr_koltuk[0]["eventSeatId"]])
    kontrol(
        "Belgesiz ogrenci bileti 409 + StudentVerificationRequired",
        c.status_code == 409 and kod(c) == "Reservation.StudentVerificationRequired",
        f"{c.status_code} {kod(c)}",
    )

    c = cagir(
        "POST",
        "/student-verifications",
        token=ogr,
        govde={
            "fullName": "Ogrenci Test",
            "institutionName": f"Istanbul Teknik Universitesi {v['ek']}",
            "studentNumber": f"150{v['ek'][:6]}",
            "validUntilUtc": "2027-06-30T00:00:00Z",
            "nationalIdentityNumber": None,
            "documentFileId": None,
        },
    )
    kontrol(
        "Kimlik numarasiz ogrenci kaydi kabul ediliyor",
        c.status_code in (200, 201),
        f"{c.status_code} {c.text[:160]}",
    )
    dogrulama_id = c.json() if isinstance(c.json(), str) else c.json().get("id")

    c = rezerve_et(ogr, oturum, [ogr_koltuk[0]["eventSeatId"]])
    kontrol(
        "Onaylanmamis belge yeterli degil 409",
        c.status_code == 409 and kod(c) == "Reservation.StudentVerificationRequired",
        f"{c.status_code} {kod(c)}",
    )

    c = cagir(
        "POST",
        f"/student-verifications/{dogrulama_id}/review",
        token=v["admin"],
        govde={"approve": True, "rejectionReason": None},
    )
    c.raise_for_status()

    c = rezerve_et(ogr, oturum, [ogr_koltuk[0]["eventSeatId"]])
    kontrol(
        "Onayli belgeyle ogrenci bileti aliniyor",
        c.status_code == 200,
        f"{c.status_code} {c.text[:160]}",
    )
    kontrol(
        "Ogrenci fiyati koltuktan geldi (120)",
        c.status_code == 200 and c.json()["totalAmount"] == 120,
        c.json().get("totalAmount") if c.status_code == 200 else "-",
    )

    # ------------------------------------------------------------------
    baslik("10 · YARIS DURUMU — 50 paralel istek, ayni koltuk")

    hedef = musait(oturum, "Balkon", 1)[0]["eventSeatId"]

    print("  50 kullanici olusturuluyor...")
    with ThreadPoolExecutor(max_workers=16) as havuz:
        tokenlar = list(
            havuz.map(
                lambda i: kayit(f"yaris{i}.{v['ek']}@loca.dev", f"Yarisci {i}"),
                range(50),
            )
        )

    def dene(token):
        try:
            c = rezerve_et(token, oturum, [hedef])
            return c.status_code, kod(c)
        except Exception as hata:  # pragma: no cover
            return 0, str(hata)

    baslangic = time.perf_counter()
    with ThreadPoolExecutor(max_workers=50) as havuz:
        sonuclar = list(havuz.map(dene, tokenlar))
    sure = (time.perf_counter() - baslangic) * 1000

    basarili = [s for s in sonuclar if s[0] == 200]
    cakisan = [s for s in sonuclar if s[0] == 409]
    diger = [s for s in sonuclar if s[0] not in (200, 409)]

    print(f"  Sure: {sure:.0f} ms")
    print(f"  Dagilim: 200 -> {len(basarili)}, 409 -> {len(cakisan)}, diger -> {len(diger)}")
    kodlar = {}
    for durum, k in cakisan:
        kodlar[k] = kodlar.get(k, 0) + 1
    print(f"  409 kod dagilimi: {json.dumps(kodlar, ensure_ascii=False)}")

    kontrol("TAM OLARAK 1 basarili", len(basarili) == 1, len(basarili))
    kontrol("Kalan 49 cakisma", len(cakisan) == 49, len(cakisan))
    kontrol("Beklenmeyen durum kodu yok", len(diger) == 0, diger[:5])

    son = {k["eventSeatId"]: k for k in koltuklar(oturum, None, "Balkon")}
    kontrol("Koltuk Locked durumda", son[hedef]["status"] == "Locked", son[hedef]["status"])

    # ------------------------------------------------------------------
    if not HIZLI:
        baslik("11 · Sure dolumu koltugu geri veriyor")

        bekleme = 75
        print(f"  Kilit suresinin dolmasi ve temizlik turu bekleniyor ({bekleme} sn)...")
        time.sleep(bekleme)

        c = cagir("GET", f"/reservations/{b_rez}", token=b)
        kontrol(
            "Suresi dolan rezervasyon Expired",
            c.status_code == 200 and c.json()["status"] == "Expired",
            c.json().get("status") if c.status_code == 200 else c.status_code,
        )
        kontrol(
            "Kalan sure sifir",
            c.status_code == 200 and c.json()["remainingSeconds"] == 0,
            c.json().get("remainingSeconds") if c.status_code == 200 else "-",
        )

        geri = {k["eventSeatId"]: k for k in koltuklar(oturum, None, "Balkon")}
        kontrol(
            "Yaris testindeki koltuk yeniden Available",
            geri[hedef]["status"] == "Available",
            geri[hedef]["status"],
        )

        c = cagir("POST", f"/reservations/{b_rez}/extend", token=b)
        kontrol("Suresi dolmus rezervasyon uzatilamaz 409", c.status_code == 409, c.status_code)

    # ------------------------------------------------------------------
    print("\n" + "=" * 62)
    print(f"SONUC: {gecen} gecti, {kalan} kaldi  (toplam {gecen + kalan})")
    if hatalar:
        print("\nBasarisiz kontroller:")
        for h in hatalar:
            print(f"  - {h}")
    print(f"\nOturum: {oturum}")
    return 0 if kalan == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
