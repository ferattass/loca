# Loca arayuzu — derle, sonra statik dosya olarak servis et.
#
# Uretimde Vite'in kendi gelistirme sunucusu KULLANILMIYOR: o sunucu
# kaynak haritalari ve modulleri tek tek servis ediyor, sicak yeniden
# yukleme icin bir websocket aciyor ve hicbir onbellek basligi koymuyor.

# --- Derleme -----------------------------------------------------------
FROM node:24-alpine AS derleme
WORKDIR /kaynak

# Once yalnizca kilit dosyasi: kaynak degistiginde npm ci katmani
# gecersiz olmasin.
COPY package.json package-lock.json ./

# ci, install degil: kilit dosyasina birebir uyuyor ve uymuyorsa
# sessizce guncellemek yerine hata veriyor. Imajda kurulan surumler
# yereldekiyle ayni olmali.
RUN npm ci

COPY . .

# API adresi DERLEME ANINDA gomuluyor. Vite ortam degiskenlerini paket
# icine yaziyor; calisma aninda okunamiyor. Bu yuzden farkli ortam =
# farkli imaj. Alternatifi adresi calisma aninda bir yapilandirma
# dosyasindan okumak olurdu; bu proje icin gereksiz karmasik.
ARG VITE_API_BASE_URL=/api/v1
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

RUN npm run build

# --- Servis ------------------------------------------------------------
FROM nginx:alpine AS calisma

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=derleme /kaynak/dist /usr/share/nginx/html

EXPOSE 80

# ADRES "localhost" DEGIL "127.0.0.1".
# Konteyner icinde localhost once ::1'e (IPv6) cozuluyor; nginx ise
# `listen 80;` ile yalnizca IPv4 dinliyor ve baglanti reddediliyor.
# Sonuc: nginx sorunsuz calisip istekleri yanitlarken konteyner
# "unhealthy" gorunuyordu — uretimde bu, calisan bir konteynerin
# yonlendirici tarafindan olu sayilip yeniden baslatilmasi demek.
HEALTHCHECK --interval=15s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://127.0.0.1/ || exit 1
