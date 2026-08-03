# -*- coding: utf-8 -*-
"""Gun 7 uctan uca dogrulama: odeme, bilet ve outbox.

Gun 6'nin kurulum kodunu yeniden kullaniyor. Gercek istek, gercek
veritabani, gercek odeme saglayicisi (taklit).
"""

import subprocess
import sys
import uuid

from e2e_gun6 import (
    cagir,
    giris,
    kayit,
    kod,
    kurulum,
    musait,
    rezerve_et,
    SIFRE,
)

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


def psql(sql):
    return subprocess.run(
        ["docker", "exec", "loca-postgres", "psql", "-U", "loca_user", "-d", "loca",
         "-t", "-A", "-c", sql],
        capture_output=True, text=True, check=False,
    ).stdout.strip()


def odeme_baslat(token, rezervasyon_id, anahtar=None):
    return cagir(
        "POST",
        "/payments",
        token=token,
        govde={"reservationId": rezervasyon_id},
        basliklar={"Idempotency-Key": anahtar or str(uuid.uuid4())},
    )


def main():
    print("Gun 7 — odeme, bilet ve outbox, uctan uca dogrulama")
    print("=" * 62)

    baslik("Kurulum")
    v = kurulum()
    oturum = v["oturum_id"]
    kontrol("Etkinlik yayina alindi", v["uretilen_koltuk"] == 440, v["uretilen_koltuk"])

    eposta = f"od.{v['ek']}@loca.dev"
    kayit(eposta, "Odeme Musterisi")
    musteri = giris(eposta, SIFRE)

    # ------------------------------------------------------------------
    baslik("1 · Rezervasyon → odeme → bilet")

    koltuklar = musait(oturum, "Orta", 2)
    idler = [k["eventSeatId"] for k in koltuklar]

    c = rezerve_et(musteri, oturum, idler)
    kontrol("Rezervasyon acildi", c.status_code == 200, f"{c.status_code} {c.text[:160]}")
    rez = c.json()
    rez_id = rez["id"]

    c = odeme_baslat(musteri, rez_id)
    kontrol("Odeme baslatildi", c.status_code == 200, f"{c.status_code} {c.text[:200]}")
    odeme = c.json()
    odeme_id = odeme["id"]

    kontrol(
        "Tutar rezervasyondan kopyalandi (900)",
        odeme["amount"] == 900,
        odeme.get("amount"),
    )
    kontrol("Durum Pending", odeme["status"] == "Pending", odeme["status"])
    kontrol(
        "Saglayici kimligi kaydedildi",
        bool(odeme.get("providerReference")),
        odeme.get("providerReference"),
    )

    c = cagir("POST", f"/payments/{odeme_id}/complete", token=musteri)
    kontrol("Odeme tamamlandi", c.status_code == 200, f"{c.status_code} {c.text[:200]}")
    sonuc = c.json()

    kontrol("Durum Succeeded", sonuc["status"] == "Succeeded", sonuc["status"])
    kontrol("Durum degisti isareti true", sonuc["stateChanged"] is True, sonuc["stateChanged"])
    kontrol("Iki bilet uretildi", len(sonuc["tickets"]) == 2, len(sonuc["tickets"]))

    biletler = sonuc["tickets"]
    kontrol(
        "Bilet numarasi ve QR dolu",
        all(b["ticketNumber"] and b["qrCode"] for b in biletler),
        biletler[0] if biletler else None,
    )
    kontrol(
        "Bilet numaralari birbirinden farkli",
        len({b["ticketNumber"] for b in biletler}) == 2,
        [b["ticketNumber"] for b in biletler],
    )
    kontrol(
        "QR kodlari birbirinden farkli",
        len({b["qrCode"] for b in biletler}) == 2,
        "-",
    )
    kontrol(
        "Bilet kesildigi andaki bilgileri tasiyor",
        all(b["eventTitle"] and b["seatLabel"] and b["ticketTypeName"] for b in biletler),
        biletler[0],
    )

    # ------------------------------------------------------------------
    baslik("2 · Callback idempotent")

    c = cagir("POST", f"/payments/{odeme_id}/complete", token=musteri)
    kontrol("Ikinci callback 200 donuyor", c.status_code == 200, c.status_code)
    ikinci = c.json()
    kontrol(
        "Durum degismedi isareti false",
        ikinci["stateChanged"] is False,
        ikinci["stateChanged"],
    )
    kontrol(
        "Ikinci callback yeni bilet URETMEDI",
        len(ikinci["tickets"]) == 2,
        len(ikinci["tickets"]),
    )

    bilet_sayisi = psql(
        f"""select count(*) from "Tickets" where "ReservationId" = '{rez_id}';"""
    )
    kontrol("Veritabaninda tam iki bilet var", bilet_sayisi == "2", bilet_sayisi)

    # ------------------------------------------------------------------
    baslik("3 · Koltuklar ve rezervasyon durumu")

    durum = psql(
        f"""select distinct "Status" from "EventSeats"
            where "ReservationId" = '{rez_id}';"""
    )
    kontrol("Koltuklar Sold (4)", durum == "4", durum)

    rez_durum = psql(f"""select "Status" from "Reservations" where "Id" = '{rez_id}';""")
    kontrol("Rezervasyon Confirmed (2)", rez_durum == "2", rez_durum)

    kalanlar = musait(oturum, "Orta", 1)
    kontrol(
        "Satilan koltuk artik musait listesinde yok",
        all(k["eventSeatId"] not in idler for k in kalanlar),
        "-",
    )

    # ------------------------------------------------------------------
    baslik("4 · Ayni rezervasyona ikinci odeme")

    c = odeme_baslat(musteri, rez_id)
    kontrol(
        "Odenmis rezervasyona ikinci odeme 409 + AlreadyPaid",
        c.status_code == 409 and kod(c) == "Payment.AlreadyPaid",
        f"{c.status_code} {kod(c)}",
    )

    # ------------------------------------------------------------------
    baslik("5 · Idempotency anahtari")

    yeni = musait(oturum, "Balkon", 1)
    c = rezerve_et(musteri, oturum, [yeni[0]["eventSeatId"]])
    ikinci_rez = c.json()["id"]

    anahtar = str(uuid.uuid4())
    a1 = odeme_baslat(musteri, ikinci_rez, anahtar)
    a2 = odeme_baslat(musteri, ikinci_rez, anahtar)
    kontrol(
        "Ayni anahtarla ikinci istek ayni odemeyi donuyor",
        a1.status_code == 200 and a2.status_code == 200 and a1.json()["id"] == a2.json()["id"],
        f"{a1.status_code}/{a2.status_code}",
    )

    # ------------------------------------------------------------------
    baslik("6 · Outbox")

    outbox = psql(
        f"""select "Type" || ':' || (case when "ProcessedAtUtc" is null then 'bekliyor' else 'islendi' end)
            from "OutboxMessages" where "CorrelationId" = '{rez_id}';"""
    )
    kontrol("Bilet uretimi outbox'a yazildi", "TicketsIssued" in outbox, outbox)

    payload_tur = psql(
        """select data_type from information_schema.columns
           where table_name = 'OutboxMessages' and column_name = 'Payload';"""
    )
    kontrol("Payload jsonb olarak saklaniyor", payload_tur == "jsonb", payload_tur)

    # ------------------------------------------------------------------
    baslik("7 · Yetki")

    baska_eposta = f"bsk.{v['ek']}@loca.dev"
    kayit(baska_eposta, "Baska Musteri")
    baska = giris(baska_eposta, SIFRE)

    c = cagir("GET", f"/payments/{odeme_id}", token=baska)
    kontrol(
        "Baskasinin odemesi 403",
        c.status_code == 403 and kod(c) == "Payment.NotOwner",
        f"{c.status_code} {kod(c)}",
    )

    c = cagir("POST", f"/payments/{odeme_id}/refund", token=musteri, govde={"reason": "deneme"})
    kontrol("Musteri iade edemiyor 403", c.status_code == 403, c.status_code)

    # ------------------------------------------------------------------
    baslik("8 · Iade")

    c = cagir(
        "POST",
        f"/payments/{odeme_id}/refund",
        token=v["admin"],
        govde={"reason": "Etkinlik iptal edildi"},
    )
    kontrol("Admin iade edebiliyor 204", c.status_code == 204, f"{c.status_code} {c.text[:160]}")

    odeme_durum = psql(f"""select "Status" from "Payments" where "Id" = '{odeme_id}';""")
    kontrol("Odeme Refunded (4)", odeme_durum == "4", odeme_durum)

    bilet_durum = psql(
        f"""select distinct "Status" from "Tickets" where "ReservationId" = '{rez_id}';"""
    )
    kontrol("Biletler Refunded (4)", bilet_durum == "4", bilet_durum)

    koltuk_durum = psql(
        f"""select count(*) from "EventSeats" where "Id" in ('{idler[0]}','{idler[1]}')
            and "Status" = 1;"""
    )
    kontrol("Koltuklar satisa geri acildi", koltuk_durum == "2", koltuk_durum)

    c = cagir("POST", f"/payments/{odeme_id}/refund", token=v["admin"], govde={"reason": "tekrar"})
    kontrol("Ikinci iade 409", c.status_code == 409, c.status_code)

    # ------------------------------------------------------------------
    print("\n" + "=" * 62)
    print(f"SONUC: {gecen} gecti, {kalan} kaldi  (toplam {gecen + kalan})")
    if hatalar:
        print("\nBasarisiz kontroller:")
        for h in hatalar:
            print(f"  - {h}")
    return 0 if kalan == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
