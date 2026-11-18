#!/usr/bin/env bash
set -euo pipefail

if [[ ! -f "JQuery-ECommerceAss.csproj" ]]; then
    echo "Run this script from /workspaces/eduvo-mvc-modernization." >&2
    exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
    echo "Docker is required to start disposable SQL Server and Mailpit." >&2
    exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
    echo ".NET SDK is required." >&2
    exit 1
fi

export DOTNET_ROLL_FORWARD="${DOTNET_ROLL_FORWARD:-Major}"

if ! DOTNET_ROLL_FORWARD=Major dotnet tool run dotnet-ef --version >/dev/null 2>&1; then
    echo "Restoring the repository-local dotnet-ef tool..."
    DOTNET_ROLL_FORWARD=Major dotnet tool restore
fi

export Smtp__Host="${Smtp__Host:-127.0.0.1}"
export Smtp__Port="${Smtp__Port:-1025}"
export Smtp__From="${Smtp__From:-no-reply@example.test}"
export Smtp__Username="${Smtp__Username:-}"
export Smtp__Password="${Smtp__Password:-}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:5067}"

sql_started=false
mailpit_started=false

if docker ps --format '{{.Names}}' | grep -qx 'eduvo-sql'; then
    echo "Eduvo SQL Server is already running. Stop eduvo-sql and rerun for a clean disposable database." >&2
    exit 1
fi
if docker ps -a --format '{{.Names}}' | grep -qx 'eduvo-sql'; then
    echo "Removing the stopped eduvo-sql container."
    docker rm eduvo-sql >/dev/null
fi

sql_password="Eduvo_${RANDOM}_${RANDOM}_Aa1!"
docker run --rm --name eduvo-sql \
    -e ACCEPT_EULA=Y \
    -e MSSQL_SA_PASSWORD="${sql_password}" \
    -p 127.0.0.1:1433:1433 \
    mcr.microsoft.com/mssql/server:2022-latest >/tmp/eduvo-sql.log 2>&1 &
sql_pid=$!
sql_started=true

sql_connection="Server=127.0.0.1,1433;Database=EduvoDemo;User Id=sa;Password=${sql_password};TrustServerCertificate=True"

echo "Waiting for disposable SQL Server..."
sql_ready=false
for attempt in {1..60}; do
    if docker exec eduvo-sql /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P "${sql_password}" -C -Q 'SELECT 1' >/dev/null 2>&1; then
        sql_ready=true
        break
    fi
    if [[ "${attempt}" == 60 ]]; then
        break
    fi
    sleep 2
done

if [[ "${sql_ready}" != true ]]; then
    echo "Disposable SQL Server did not become ready. See /tmp/eduvo-sql.log." >&2
    exit 1
fi

if docker ps --format '{{.Names}}' | grep -qx 'eduvo-mailpit'; then
    echo "Using the existing eduvo-mailpit container."
else
    if docker ps -a --format '{{.Names}}' | grep -qx 'eduvo-mailpit'; then
        echo "Removing the stopped eduvo-mailpit container."
        docker rm eduvo-mailpit >/dev/null
    fi

    docker run --rm --name eduvo-mailpit \
        -p 127.0.0.1:1025:1025 \
        -p 127.0.0.1:8025:8025 \
        axllent/mailpit >/tmp/eduvo-mailpit.log 2>&1 &
    mailpit_pid=$!
    mailpit_started=true
fi

cleanup() {
    if [[ "${sql_started}" == true ]]; then
        docker stop eduvo-sql >/dev/null 2>&1 || true
        docker rm -f eduvo-sql >/dev/null 2>&1 || true
        kill "${sql_pid:-0}" >/dev/null 2>&1 || true
    fi
    if [[ "${mailpit_started}" == true ]]; then
        docker stop eduvo-mailpit >/dev/null 2>&1 || true
        docker rm -f eduvo-mailpit >/dev/null 2>&1 || true
        kill "${mailpit_pid:-0}" >/dev/null 2>&1 || true
    fi
}
trap cleanup EXIT INT TERM

for attempt in {1..20}; do
    if curl --silent --fail --head http://127.0.0.1:8025 >/dev/null; then
        break
    fi

    if [[ "${attempt}" == 20 ]]; then
        echo "Mailpit did not become ready. See /tmp/eduvo-mailpit.log." >&2
        exit 1
    fi
    sleep 1
done

echo "Applying disposable database migrations..."
env "ConnectionStrings__JQuery-ECommerceAss=${sql_connection}" \
    dotnet tool run dotnet-ef database update --project JQuery-ECommerceAss.csproj

demo_admin_password='Admin123!'
demo_seller_password='Seller123!'
demo_shopper_password='Shopper123!'

hash_password() {
    python3 - "$1" <<'PY'
import base64
import hashlib
import os
import sys

password = sys.argv[1].encode("utf-8")
iterations = 100000
salt = os.urandom(16)
digest = hashlib.pbkdf2_hmac("sha256", password, salt, iterations, 32)
print(
    f"PBKDF2-SHA256${iterations}$"
    f"{base64.b64encode(salt).decode('ascii')}$"
    f"{base64.b64encode(digest).decode('ascii')}"
)
PY
}

admin_hash=$(hash_password "${demo_admin_password}")
seller_hash=$(hash_password "${demo_seller_password}")
shopper_hash=$(hash_password "${demo_shopper_password}")

docker exec eduvo-sql /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "${sql_password}" -d EduvoDemo -C \
    -Q "
IF NOT EXISTS (SELECT 1 FROM [Admins] WHERE [AdminUsername] = N'demo-admin')
BEGIN
    INSERT INTO [Admins] ([AdminUsername], [AdminPassword], [DateTime])
    VALUES (N'demo-admin', N'${admin_hash}', NULL);
END;

IF NOT EXISTS (SELECT 1 FROM [Sellers] WHERE [SellerUsername] = N'demo-seller')
BEGIN
    INSERT INTO [Sellers]
        ([SellerUsername], [SellerPassword], [SellerEmail], [LoginTry], [SellerStatus], [DateTime])
    VALUES
        (N'demo-seller', N'${seller_hash}', N'demo-seller@example.test', 0, N'Approve', NULL);
END;

DECLARE @sellerId int = (SELECT TOP 1 [SellerID] FROM [Sellers] WHERE [SellerUsername] = N'demo-seller');

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Username] = N'demo-shopper')
BEGIN
    INSERT INTO [Users]
        ([Username], [UserPassword], [UserGmail], [LoginTry], [UserPhoneNum], [DateTime], [RegisterDate], [UserStatus])
    VALUES
        (N'demo-shopper', N'${shopper_hash}', N'demo-shopper@example.test', 0, N'5550100', NULL, GETUTCDATE(), N'Active');
END;

IF NOT EXISTS (SELECT 1 FROM [Products] WHERE [ProductName] = N'Demo Laptop')
BEGIN
    INSERT INTO [Products]
        ([ProductName], [Description], [UnitPrice], [Quantity], [SellerID], [ProductStatus], [ImagePath])
    VALUES
        (N'Demo Laptop', N'Disposable demo product', 2499.00, 10, @sellerId, N'Active', NULL);
END;
" >/dev/null

cat <<'EOF'

Eduvo demo is ready:
  Application: http://localhost:5067
  Admin:       http://localhost:5067/Admin/Login
  Seller:      http://localhost:5067/Seller/Login
  Shopper:     http://localhost:5067/User/Login
  Mailpit:     http://localhost:8025

Disposable login:
  Admin:   demo-admin / Admin123!
  Seller:  demo-seller / Seller123!
  Shopper: demo-shopper / Shopper123!
  Shop mail: demo-shopper@example.test

Keep this terminal open while recording. Press Ctrl+C to stop the app and the
Mailpit container started by this script.
EOF

env "ConnectionStrings__JQuery-ECommerceAss=${sql_connection}" \
    dotnet run --no-launch-profile --project JQuery-ECommerceAss.csproj
