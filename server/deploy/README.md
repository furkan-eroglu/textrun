# Canlı sunucu

- Sunucu: Hetzner CX23, Nürnberg, Ubuntu 24.04. Dosyalar `/opt/textrun` içinde.
- Bağlanma: `ssh -i ~/.ssh/textrun_hetzner root@2.31.32.138`
- Adres: `https://2-31-32-138.sslip.io` (şifreli; sertifikayı Caddy alır ve yeniler).
- Gizli değerler sunucuda `/opt/textrun/.env` içinde (depoda yok). Yönetim paneli şifresi de orada (`CONSOLE_PASSWORD`, kullanıcı `admin`).
- Yönetim paneli dışarıya kapalı. Açmak için: `ssh -i ~/.ssh/textrun_hetzner -L 7351:127.0.0.1:7351 root@2.31.32.138`, sonra tarayıcıda `http://127.0.0.1:7351`.
- Yeni sunucu kodu yüklemek: `server` klasöründe `npm run build`, sonra `build/index.js` dosyasını `/opt/textrun/modules/` içine kopyala ve `docker compose -f docker-compose.prod.yml restart nakama`.
- Yedek: her gece 03:30, `/opt/textrun/backups` (son 7 gün).
