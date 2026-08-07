# -*- coding: utf-8 -*-
"""Her rolden demo hesabi acar.

Teslim gosteriminde jurinin dort ayri ekrani gormesi gerekiyor: musteri
vitrini, organizator sihirbazi, onay kuyrugu ve yonetim paneli. Tek bir
admin hesabiyla gezmek her ekrani ACIYOR ama hicbirini gercekte
oldugu gibi gostermiyor — admin zaten her seyi gorduğu icin rol
kisitlarinin calistigi anlasilamiyor.

Sifreler BILEREK sabit ve basit: bunlar yalnizca demo hesaplari ve
teslimde ekrandan okunacaklar. Gercek kullanici hesaplarinda sifre
sunucuda uretiliyor ve panelde hic gorunmuyor
(POST /admin/users — CreateUserCommand).

Betik TEKRARLANABILIR: var olan hesabi yeniden acmaya calismiyor,
yalnizca rollerini tamamliyor.

Kosum (API ayakta olmali):
    python tools/demo-veri/hesaplari_doldur.py
"""

import json
import os
import sys
import urllib.error
import urllib.request

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
ORTAK_SIFRE = "Loca!Demo2026"

# E-posta, ad soyad, roller.
HESAPLAR = [
    ("musteri@loca.dev", "Deniz Müşteri", ["Customer"]),
    ("organizator@loca.dev", "Ece Organizatör", ["Customer", "Organizer"]),
    ("kapi@loca.dev", "Kapı Görevlisi", ["Customer", "Organizer"]),
    ("moderator@loca.dev", "Mert Moderatör", ["Customer", "Moderator"]),
    ("sanatci@loca.dev", "Selin Sanatçı", ["Customer", "Organizer"]),
    ("ogrenci@loca.dev", "Onur Öğrenci", ["Customer"]),
]


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
    return 200 <= durum < 300


def main():
    durum, cevap = istek("/auth/login", "POST",
                         {"email": ADMIN[0], "password": ADMIN[1]})
    if not basarili(durum):
        print(f"Admin girisi basarisiz: {durum} {cevap}")
        return 1
    token = cevap["accessToken"]

    durum, liste = istek("/admin/users?pageSize=200", token=token)
    if not basarili(durum):
        print(f"Kullanicilar alinamadi: {durum} {liste}")
        return 1

    mevcut = {k["email"]: k for k in liste["items"]}

    for eposta, ad, roller in HESAPLAR:
        kayit = mevcut.get(eposta)

        if kayit is None:
            # Kayit ucu kullaniliyor, admin hesap acma ucu DEGIL: o uc
            # sifreyi sunucuda uretip postayla gonderiyor ve demo
            # hesabina giris yapilamazdi.
            durum, cevap = istek("/auth/register", "POST", {
                "email": eposta,
                "password": ORTAK_SIFRE,
                "fullName": ad,
                "phoneNumber": None,
            })

            if not basarili(durum):
                print(f"[hata] {eposta} -> {durum} {cevap}")
                continue

            durum, liste = istek("/admin/users?pageSize=200", token=token)
            mevcut = {k["email"]: k for k in liste["items"]}
            kayit = mevcut.get(eposta)
            print(f"[acildi] {eposta}")
        else:
            print(f"[var]    {eposta}")

        if kayit is None:
            continue

        for rol in roller:
            if rol in kayit["roles"]:
                continue

            durum, cevap = istek(f"/admin/users/{kayit['id']}/roles", "POST",
                                 {"roleName": rol, "grant": True}, token)

            if basarili(durum):
                print(f"           + {rol}")
            else:
                print(f"           ! {rol} verilemedi: {durum} {cevap}")

    print("\nDemo hesaplari:")
    print(f"  {'E-posta':<26} {'Sifre':<18} Rol")
    print(f"  {ADMIN[0]:<26} {ADMIN[1]:<18} Admin")

    for eposta, _, roller in HESAPLAR:
        print(f"  {eposta:<26} {ORTAK_SIFRE:<18} {', '.join(roller)}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
