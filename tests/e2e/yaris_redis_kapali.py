# -*- coding: utf-8 -*-
"""Yaris durumu testi — Redis ACIK ve KAPALI iken.

Neden iki kez calisiyor: tam senaryoda 49 cakismanin tamamini Redis
yakaladi. Yani "50 paralel istekte 1 basari" olcutu gecti ama VERITABANI
savunmasi hic sinanmadi. Redis kapatilip ayni test tekrarlandiginda
katmanli savunmanin ikinci halkasi (transaction + xmin damgasi) tek basina
olculuyor.

Ayrica sartnamenin "Redis kapaliyken sistem cokmemeli" maddesi de burada
dogrulaniyor.
"""

import json
import subprocess
import sys
import time
import uuid
from concurrent.futures import ThreadPoolExecutor

from e2e_gun6 import giris, kayit, kod, koltuklar, kurulum, musait, rezerve_et


def docker(*args):
    return subprocess.run(
        ["docker", *args], capture_output=True, text=True, check=False
    ).stdout.strip()


def psql(sql):
    return subprocess.run(
        ["docker", "exec", "loca-postgres", "psql", "-U", "loca_user", "-d", "loca",
         "-t", "-A", "-c", sql],
        capture_output=True, text=True, check=False,
    ).stdout.strip()


def yaris(tokenlar, oturum, koltuk_id, etiket):
    def dene(token):
        try:
            c = rezerve_et(token, oturum, [koltuk_id])
            return c.status_code, kod(c)
        except Exception as hata:
            return 0, str(hata)[:80]

    baslangic = time.perf_counter()
    with ThreadPoolExecutor(max_workers=len(tokenlar)) as havuz:
        sonuclar = list(havuz.map(dene, tokenlar))
    sure = (time.perf_counter() - baslangic) * 1000

    basarili = [s for s in sonuclar if s[0] == 200]
    cakisan = [s for s in sonuclar if s[0] == 409]
    diger = [s for s in sonuclar if s[0] not in (200, 409)]

    dagilim = {}
    for _, k in cakisan:
        dagilim[k] = dagilim.get(k, 0) + 1

    print(f"\n--- {etiket} ---")
    print(f"  Sure          : {sure:.0f} ms")
    print(f"  200 (basarili): {len(basarili)}")
    print(f"  409 (cakisma) : {len(cakisan)}")
    print(f"  diger         : {len(diger)}  {diger[:3] if diger else ''}")
    print(f"  409 kodlari   : {json.dumps(dagilim, ensure_ascii=False)}")

    return len(basarili), len(cakisan), len(diger), dagilim


def main():
    print("Yaris durumu — Redis acik / kapali karsilastirmasi")
    print("=" * 62)

    v = kurulum()
    oturum = v["oturum_id"]
    print(f"Kurulum tamam. Oturum: {oturum}")

    print("50 kullanici olusturuluyor...")
    with ThreadPoolExecutor(max_workers=16) as havuz:
        tokenlar = list(
            havuz.map(
                lambda i: kayit(f"y{i}.{v['ek']}@loca.dev", f"Yarisci {i}"), range(50)
            )
        )

    sonuc = {"gecen": 0, "kalan": 0}

    def kontrol(ad, kosul, ayrinti=""):
        if kosul:
            sonuc["gecen"] += 1
            print(f"  [OK]   {ad}")
        else:
            sonuc["kalan"] += 1
            print(f"  [HATA] {ad} -> {ayrinti}")

    # --- 1 · Redis ACIK ---------------------------------------------------
    hedef1 = musait(oturum, "Balkon", 1)[0]["eventSeatId"]
    b1, c1, d1, dag1 = yaris(tokenlar, oturum, hedef1, "REDIS ACIK")
    kontrol("Redis acik: tam olarak 1 basari", b1 == 1, b1)
    kontrol("Redis acik: 49 cakisma", c1 == 49, c1)
    kontrol(
        "Redis acik: cakismalar on elemede yakalandi",
        dag1.get("Reservation.SeatNotAvailable", 0) >= 40,
        dag1,
    )

    # --- 2 · Redis KAPALI -------------------------------------------------
    print("\nRedis durduruluyor...")
    docker("stop", "loca-redis")
    time.sleep(2)

    hedef2 = musait(oturum, "Balkon", 1)[0]["eventSeatId"]

    # Sistem cokmedi mi: tek bir istek once denenir.
    tek = rezerve_et(tokenlar[0], oturum, [musait(oturum, "Orta", 1)[0]["eventSeatId"]])
    kontrol(
        "Redis kapaliyken sistem calismaya devam ediyor (500 donmuyor)",
        tek.status_code == 200,
        f"{tek.status_code} {tek.text[:160]}",
    )

    b2, c2, d2, dag2 = yaris(tokenlar, oturum, hedef2, "REDIS KAPALI")
    kontrol("Redis kapali: TAM OLARAK 1 basari", b2 == 1, b2)
    kontrol("Redis kapali: 49 cakisma", c2 == 49, c2)
    kontrol("Redis kapali: beklenmeyen durum kodu yok", d2 == 0, d2)
    kontrol(
        "Redis kapali: cakismalar veritabani katmanindan geldi",
        dag2.get("Reservation.SeatTakenConcurrently", 0) > 0,
        dag2,
    )

    print("\nRedis geri baslatiliyor...")
    docker("start", "loca-redis")
    time.sleep(3)

    # --- 3 · Veritabani kaniti -------------------------------------------
    print("\n--- Veritabani dogrulamasi ---")

    for etiket, koltuk in (("Redis acik", hedef1), ("Redis kapali", koltuk_str := hedef2)):
        adet = psql(
            f"""select count(*) from "ReservationItems" i
                join "Reservations" r on r."Id" = i."ReservationId"
                where i."EventSeatId" = '{koltuk}' and r."Status" = 1;"""
        )
        durum = psql(
            f"""select "Status", "LockedByUserId" is not null
                from "EventSeats" where "Id" = '{koltuk}';"""
        )
        print(f"  {etiket}: aktif kalem = {adet}, koltuk durumu = {durum}")
        kontrol(f"{etiket}: veritabaninda TEK aktif rezervasyon", adet == "1", adet)
        kontrol(f"{etiket}: koltuk kilitli (Status=2)", durum.startswith("2|"), durum)

    toplam = psql(
        f"""select count(*) from "Reservations" r
            where r."EventSessionId" = '{oturum}' and r."Status" = 1;"""
    )
    print(f"  Oturumdaki toplam bekleyen rezervasyon: {toplam}")

    print("\n" + "=" * 62)
    print(f"SONUC: {sonuc['gecen']} gecti, {sonuc['kalan']} kaldi")
    return 0 if sonuc["kalan"] == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
