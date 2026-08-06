# Loca API — cok asamali imaj.
#
# Baglam depo KOKUNDEN veriliyor (docker-compose.yml: context: .) cunku
# cozum sekiz projeden olusuyor ve WebApi digerlerine proje referansiyla
# bagli. Baglam yalnizca src/Loca.WebApi olsaydi restore adimi referans
# verilen projeleri bulamazdi.

# --- Derleme -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS derleme
WORKDIR /kaynak

# Once YALNIZCA proje dosyalari kopyalaniyor ve restore calisiyor. Kaynak
# kodla birlikte kopyalansaydi tek satirlik bir degisiklik butun NuGet
# indirmesini gecersiz kilardi; bu hâliyle katman ancak bagimliliklar
# degisince yeniden kuruluyor.
# .editorconfig ZORUNLU. Kurallar Directory.Build.props ile hata sayiliyor
# (TreatWarningsAsErrors) ve kapatilan kurallarin gerekceleri
# .editorconfig'te duruyor. Kopyalanmazsa imaj derlemesi yerelde gecen
# kodu reddediyor: ornegin CA1716, Event tipinin adini "ayrilmis kelime"
# diye hata sayiyor. Yani yerelde derlenen ile imajda derlenen ayni kod
# olmazdi.
COPY Directory.Build.props .editorconfig Loca.sln ./
COPY src/Loca.Domain/*.csproj          src/Loca.Domain/
COPY src/Loca.Application/*.csproj     src/Loca.Application/
COPY src/Loca.Infrastructure/*.csproj  src/Loca.Infrastructure/
COPY src/Loca.Persistence/*.csproj     src/Loca.Persistence/
COPY src/Loca.WebApi/*.csproj          src/Loca.WebApi/

RUN dotnet restore src/Loca.WebApi/Loca.WebApi.csproj

COPY src/ src/

# Analyzer'lar ve TreatWarningsAsErrors burada da gecerli: yerelde
# derlenen ile imajda derlenen ayni kurallara tabi olmali, yoksa "bende
# calisiyordu" imaj kurulumunda ortaya cikardi.
RUN dotnet publish src/Loca.WebApi/Loca.WebApi.csproj \
    -c Release \
    -o /yayin \
    --no-restore

# --- Calisma -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS calisma
WORKDIR /app

# Kok kullanici DEGIL. Bir aciktan iceri girildiginde saldirganin
# konteyner icindeki yetkisi sinirli kalsin; alpine imajinda hazir gelen
# app kullanicisi (UID 1654) kullaniliyor.
#
# Yukleme klasoru imajda oluşturulup sahipligi veriliyor: named volume
# ilk baglandiginda bos ve kok sahipli geliyor, uygulama yazamazdi.
RUN mkdir -p /app/uploads && chown -R app:app /app

# Data Protection anahtarlarinin klasoru. Kalici olmasi SART: anahtar her
# yeniden baslatmada degisirse veritabaninda sifreli duran SMTP sifresi
# cozulemez hâle gelir ve yonetici onu her deploy'dan sonra yeniden
# girmek zorunda kalir. Klasor burada aciliyor, yolu compose
# DataProtection__KeyPath ile veriyor ve ayni yola volume bagliyor.
RUN mkdir -p /app/keys && chown -R app:app /app/keys

USER app

# Kestrel varsayilan olarak 8080'i dinliyor (8.0'dan beri kok olmayan
# kullanici ayricalikli portu acamadigi icin 80 degil).
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# Konteynerin hazir olup olmadigini yonlendirici bu ucla soruyor.
# Bagimliliklara bakmayan /health kullaniliyor: veritabani birkac saniye
# susunca konteyner olu sayilip yeniden baslatilmamali.
# Adres "localhost" degil "127.0.0.1": konteyner icinde localhost once
# ::1'e cozulebiliyor ve sunucu yalnizca IPv4 dinliyorsa baglanti
# reddediliyor. Calisan bir konteynerin olu sayilmasi, arayuz imajinda
# tam olarak boyle olmustu.
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://127.0.0.1:8080/health || exit 1

COPY --from=derleme --chown=app:app /yayin .

ENTRYPOINT ["dotnet", "Loca.WebApi.dll"]
