#!/bin/sh
# Sunucunun ilk kurulumu (bir kez çalıştırılır): Docker, güvenlik duvarı, gizli anahtarlar, günlük yedek.
set -e
cd /opt/textrun

if ! command -v docker >/dev/null 2>&1; then
  curl -fsSL https://get.docker.com | sh
fi

# Güvenlik duvarı: yalnızca SSH, HTTP (sertifika için) ve HTTPS
ufw allow 22/tcp >/dev/null
ufw allow 80/tcp >/dev/null
ufw allow 443/tcp >/dev/null
ufw --force enable >/dev/null

# Gizli değerler: yalnızca ilk kurulumda üretilir, sonra değişmez (değişirse oturumlar ve veritabanı erişimi bozulur)
if [ ! -f .env ]; then
  umask 077
  {
    echo "DB_PASSWORD=$(openssl rand -hex 24)"
    echo "SERVER_KEY=$(openssl rand -hex 16)"
    echo "SESSION_KEY=$(openssl rand -hex 32)"
    echo "REFRESH_KEY=$(openssl rand -hex 32)"
    echo "HTTP_KEY=$(openssl rand -hex 24)"
    echo "CONSOLE_PASSWORD=$(openssl rand -hex 16)"
    echo "CONSOLE_SIGNING_KEY=$(openssl rand -hex 32)"
  } > .env
fi

# Günlük veritabanı yedeği (03:30), son 7 gün saklanır
mkdir -p backups
cat > yedekle.sh <<'YEDEK'
#!/bin/sh
cd /opt/textrun
docker compose -f docker-compose.prod.yml exec -T postgres pg_dump -U postgres nakama | gzip > backups/nakama-$(date +%F).sql.gz
find backups -name 'nakama-*.sql.gz' -mtime +7 -delete
YEDEK
chmod +x yedekle.sh
echo "30 3 * * * root /opt/textrun/yedekle.sh" > /etc/cron.d/textrun-yedek

docker compose -f docker-compose.prod.yml up -d
