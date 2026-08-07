# Loca — TEK KONTEYNER imaji (canliya alma icin).
#
# src/Loca.WebApi/Dockerfile ve web/Dockerfile ayri ayri duruyor ve yerel
# konteyner yigininda (docker-compose --profile app) hâlâ kullaniliyor:
# orada arayuz nginx'ten, API kendi konteynerinden geliyor ve ikisi
# birbirinden bagimsiz yeniden baslatilabiliyor.
#
# UCRETSIZ BARINDIRMA KATMANI FARKLI BIR SEY ISTIYOR. Servis sayisi
# sinirli, her servis ayri bir alan adi ve ayri bir uyanma suresi demek.
# Iki servis calistirmak:
#   - CORS yapilandirmasi gerektirir (iki farkli origin),
#   - iyzico callback adresini ikinci bir alan adina bagimli kilar,
#   - ucretsiz katmanda uyuyan iki servisin ikisini birden uyandirir.
#
# Bu imaj arayuzu derleyip API'nin wwwroot'una koyuyor; Program.cs klasoru
# gorunce statik dosyalari ve SPA geri dusumunu aciyor. Tek origin, tek
# adres, CORS yok.

# --- Arayuz derlemesi --------------------------------------------------
FROM node:24-alpine AS arayuz
WORKDIR /web

COPY web/package*.json ./
RUN npm ci

COPY web/ ./

# API adresi BOS birakiliyor ve istemci kendi kokunu kullaniyor
# (client.ts'teki varsayilan). Derleme aninda bir alan adi gomulseydi imaj
# tek bir dagitima baglanirdi ve ayni imajla ikinci bir ortam kurulamazdi.
ENV VITE_API_BASE_URL=/api/v1
RUN npm run build

# --- Sunucu derlemesi --------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS derleme
WORKDIR /kaynak

# .editorconfig ZORUNLU: kapatilan analyzer kurallarinin gerekceleri orada
# ve TreatWarningsAsErrors acik. Kopyalanmazsa yerelde gecen kod imajda
# reddediliyor (Gun 10'da yasandi: CA1716, "Event" ayrilmis kelime).
COPY Directory.Build.props .editorconfig Loca.sln ./
COPY src/Loca.Domain/*.csproj          src/Loca.Domain/
COPY src/Loca.Application/*.csproj     src/Loca.Application/
COPY src/Loca.Infrastructure/*.csproj  src/Loca.Infrastructure/
COPY src/Loca.Persistence/*.csproj     src/Loca.Persistence/
COPY src/Loca.WebApi/*.csproj          src/Loca.WebApi/

RUN dotnet restore src/Loca.WebApi/Loca.WebApi.csproj

COPY src/ src/

RUN dotnet publish src/Loca.WebApi/Loca.WebApi.csproj \
    -c Release \
    -o /yayin \
    --no-restore

# --- Calisma -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS calisma
WORKDIR /app

RUN mkdir -p /app/uploads /app/keys && chown -R app:app /app
USER app

COPY --from=derleme --chown=app:app /yayin .
COPY --from=arayuz  --chown=app:app /web/dist ./wwwroot

# Saglayici PORT ortam degiskeni veriyor; BarindirmaAyarlari onu okuyup
# Kestrel'e bagliyor. Yerelde calistirilirsa 8080 kullaniliyor.
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "Loca.WebApi.dll"]
