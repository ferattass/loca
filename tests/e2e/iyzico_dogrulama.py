# -*- coding: utf-8 -*-
"""Iyzico saglayicisinin GERCEKTEN cagrilip cagrilmadigi.

Bu betik "odeme calisiyor mu" diye sormuyor — mock saglayiciyla kosan
butun testler zaten yesil. Sordugu sey su: `Payment:Provider = Iyzico`
yazildiginda istek gercekten Iyzico'ya CIKIYOR MU.

Neden ayri bir betik gerekiyor: bir kere yazilmis, derlenen, okununca
dogru gorunen entegrasyon hic denenmemisti ve iki yerden kopuktu
(yonlendirme adresi hic uretilmiyordu, callback ucu yoktu). Fark
edilmemesinin sebebi butun uctan uca testlerin mock ile kosmasiydi:
TEST EDILEN YOL ILE URETIMDE CALISACAK YOL AYNI DEGILDI.

Iki kipte de anlamli sonuc veriyor:

  * GERCEK anahtarlarla  → Iyzico'nun odeme sayfasi adresi donmeli
  * SAHTE anahtarlarla   → Iyzico'dan bir HATA KODU donmeli

Ikisi de "istek karsi tarafa ulasti" demek. Basarisiz sayilan tek durum,
istegin hic gitmedigini gosteren durum: ne adres ne de saglayicidan gelen
bir hata var.

Kosmadan once API su ortam degiskenleriyle ayaga kaldirilmali:

    $env:Payment__Provider = "Iyzico"
    $env:Iyzico__ApiKey    = "sandbox-..."
    $env:Iyzico__SecretKey = "sandbox-..."
"""

import subprocess
import sys
import uuid

from e2e_gun6 import cagir, giris, kayit, kurulum, musait, rezerve_et, SIFRE

# Iyzico'ya gercekten gidildiginde donen hata metinleri saglayicidan
# geliyor ve icinde kod gecıyor. Bu iki metin ise BIZIM urettigimiz
# metinler ve "istek cikmadi" anlamina geliyor.
ULASILAMADI = "Odeme saglayicisina ulasilamadi."

gecen = 0
kalan = []


def kontrol(ad, kosul, ayrinti=""):
    global gecen
    if kosul:
        gecen += 1
        print(f"  [OK]   {ad}")
    else:
        kalan.append(f"{ad} :: {ayrinti}")
        print(f"  [HATA] {ad}  -> {ayrinti}")


def psql(sql):
    return subprocess.run(
        ["docker", "exec", "loca-postgres", "psql", "-U", "loca_user", "-d", "loca",
         "-t", "-A", "-c", sql],
        capture_output=True, text=True, check=False,
    ).stdout.strip()


def main():
    print("Iyzico entegrasyonu — istek gercekten cikiyor mu")
    print("=" * 62)

    v = kurulum()
    oturum = v["oturum_id"]

    eposta = f"iyz.{uuid.uuid4().hex[:8]}@loca.dev"
    kayit(eposta, "Iyzico Musterisi")
    musteri = giris(eposta, SIFRE)

    koltuklar = musait(oturum, "Orta", 1)
    idler = [k["eventSeatId"] for k in koltuklar]

    c = rezerve_et(musteri, oturum, idler)
    kontrol("Rezervasyon acildi", c.status_code == 200, f"{c.status_code} {c.text[:160]}")

    if c.status_code != 200:
        return 1

    rez_id = c.json()["id"]

    c = cagir(
        "POST",
        "/payments",
        token=musteri,
        govde={"reservationId": rez_id},
        basliklar={"Idempotency-Key": str(uuid.uuid4())},
    )

    print(f"\n  Cevap: {c.status_code}")
    print(f"  Govde: {c.text[:400]}\n")

    if c.status_code == 200:
        odeme = c.json()
        adres = odeme.get("redirectUrl")

        kontrol("Yonlendirme adresi dondu", bool(adres), odeme)
        # Adres kod icinde kurulmuyor, Iyzico'nun cevabindan okunuyor:
        # sandbox ile canlinin alan adlari farkli ve adresi elle kurmak
        # ortam degistiginde sessizce yanlis sayfaya gondermek olurdu.
        kontrol(
            "Adres Iyzico'nun alan adinda",
            bool(adres) and "iyzipay.com" in adres,
            adres,
        )
        kontrol(
            "Saglayici referansi (checkout token) dondu",
            bool(odeme.get("providerReference")),
            odeme.get("providerReference"),
        )
        kontrol("Saglayici Iyzico", odeme.get("provider") == "Iyzico", odeme.get("provider"))

    elif c.status_code in (409, 400, 502):
        metin = c.text

        # Iyzico'ya ULASILDI ama istegi reddetti: sahte anahtarla beklenen
        # sonuc bu. Kritik olan, hatanin SAGLAYICIDAN gelmesi.
        kontrol(
            "Hata saglayicidan geldi, agdan degil",
            ULASILAMADI not in metin,
            metin[:300],
        )

        # Musteriye saglayicinin ham mesaji GOSTERILMIYOR: hata kodu ve
        # metni saglayicinin ic isleyisini anlatiyor, musterinin
        # yapabilecegi bir sey yok ve gereksiz bilgi sizdirir.
        kontrol(
            "Musteriye genel mesaj donuyor",
            "Odeme saglayicisi islemi reddetti." in metin,
            metin[:300],
        )

        # ...ama sebep KAYBOLMUYOR. Yoksa yonetici "odemeler neden
        # basarisiz" sorusuna hicbir yerden cevap bulamazdi.
        sebep = psql(
            "SELECT \"FailureReason\" FROM \"Payments\" "
            f"WHERE \"ReservationId\" = '{rez_id}' ORDER BY \"CreatedAt\" DESC LIMIT 1;"
        )

        print(f"  Kayitli sebep: {sebep}")

        kontrol("Sebep odeme kaydina yazildi", bool(sebep), sebep)
        kontrol(
            "Sebep saglayicinin hata kodunu tasiyor",
            "kod:" in sebep,
            sebep,
        )

        print(
            "\n  NOT: Sahte anahtarla kosuldu. Bu kosu ISTEGIN CIKTIGINI\n"
            "  kanitliyor; yonlendirme adresinin dondugunu kanitlamiyor.\n"
            "  Gercek sandbox anahtariyla tekrar kosulmali."
        )
    else:
        kontrol("Beklenen bir cevap", False, f"{c.status_code} {c.text[:300]}")

    print(f"\n{gecen} gecti, {len(kalan)} kaldi")

    for satir in kalan:
        print(f"  - {satir}")

    return 0 if not kalan else 1


if __name__ == "__main__":
    sys.exit(main())
