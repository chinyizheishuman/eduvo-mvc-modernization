#!/usr/bin/env bash
set -euo pipefail

if [[ ! -f "JQuery-ECommerceAss.csproj" ]]; then
    echo "Run this script from the Eduvo repository root." >&2
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

app_port="${EDUVO_APP_PORT:-5067}"
sql_port="${EDUVO_SQL_PORT:-1433}"
smtp_port="${EDUVO_SMTP_PORT:-1025}"
mailpit_port="${EDUVO_MAILPIT_PORT:-8025}"
selected_ports=" "
for port in "$app_port" "$sql_port" "$smtp_port" "$mailpit_port"; do
    if [[ ! "$port" =~ ^[1-9][0-9]{0,4}$ ]] || (( port > 65535 )); then
        echo "Demo ports must be integers from 1 to 65535." >&2
        exit 2
    fi
    if [[ "$selected_ports" == *" $port "* ]]; then
        echo "Choose a different port for each demo service." >&2
        exit 2
    fi
    selected_ports+="$port "
done

# Check before restoring tools or starting anything so a second run leaves the demo alone.
if ! container_names=$(docker ps -a --format '{{.Names}}'); then
    echo "Docker is unavailable. Check that the daemon is running and accessible." >&2
    exit 1
fi

if grep -qx 'eduvo-sql' <<< "$container_names"; then
    echo "An eduvo-sql container already exists. Use your existing demo or a fresh environment." >&2
    exit 1
fi

published_ports=$(docker ps --format '{{.Ports}}')
if grep -q ":${sql_port}->" <<< "$published_ports"; then
    echo "Docker is already publishing host port $sql_port. Use your existing demo or a fresh environment." >&2
    exit 1
fi

running_names=$(docker ps --format '{{.Names}}')
reuse_mailpit=false
if [[ "$smtp_port" == 1025 && "$mailpit_port" == 8025 ]] && grep -qx 'eduvo-mailpit' <<< "$running_names"; then
    reuse_mailpit=true
elif grep -qx 'eduvo-mailpit' <<< "$container_names" || grep -Eq ":(${smtp_port}|${mailpit_port})->" <<< "$published_ports"; then
    echo "Mailpit's container name or host ports are already in use. Use your existing demo or a fresh environment." >&2
    exit 1
fi

export DOTNET_ROLL_FORWARD="${DOTNET_ROLL_FORWARD:-Major}"

if ! DOTNET_ROLL_FORWARD=Major dotnet tool run dotnet-ef --version >/dev/null 2>&1; then
    echo "Restoring the repository-local dotnet-ef tool..."
    DOTNET_ROLL_FORWARD=Major dotnet tool restore
fi

export Smtp__Host="${Smtp__Host:-127.0.0.1}"
export Smtp__Port="${Smtp__Port:-$smtp_port}"
export Smtp__From="${Smtp__From:-no-reply@example.test}"
export Smtp__Username="${Smtp__Username:-}"
export Smtp__Password="${Smtp__Password:-}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://127.0.0.1:$app_port}"

sql_container=""
mailpit_container=""

cleanup() {
    if [[ -n "$mailpit_container" ]]; then
        docker stop "$mailpit_container" >/dev/null 2>&1 || true
    fi
    if [[ -n "$sql_container" ]]; then
        docker stop "$sql_container" >/dev/null 2>&1 || true
    fi
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

sql_password="Eduvo_${RANDOM}_${RANDOM}_Aa1!"
sql_container=$(docker run --detach --rm --name eduvo-sql \
    -e ACCEPT_EULA=Y -e MSSQL_MEMORY_LIMIT_MB \
    -e MSSQL_SA_PASSWORD="${sql_password}" \
    -p "127.0.0.1:$sql_port:1433" \
    mcr.microsoft.com/mssql/server:2022-latest)

sql_connection="Server=127.0.0.1,$sql_port;Database=EduvoDemo;User Id=sa;Password=${sql_password};TrustServerCertificate=True"

echo "Waiting for disposable SQL Server..."
sql_ready=false
for attempt in {1..60}; do
    if docker exec "$sql_container" /opt/mssql-tools18/bin/sqlcmd \
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
    echo "Disposable SQL Server did not become ready." >&2
    exit 1
fi

if [[ "$reuse_mailpit" == true ]]; then
    echo "Using the existing eduvo-mailpit container."
else
    mailpit_container=$(docker run --detach --rm --name eduvo-mailpit \
        -p "127.0.0.1:$smtp_port:1025" \
        -p "127.0.0.1:$mailpit_port:8025" \
        axllent/mailpit)
fi

for attempt in {1..20}; do
    if curl --silent --fail --head "http://127.0.0.1:$mailpit_port" >/dev/null; then
        break
    fi

    if [[ "${attempt}" == 20 ]]; then
        echo "Mailpit did not become ready." >&2
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

docker exec "$sql_container" /opt/mssql-tools18/bin/sqlcmd \
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

INSERT INTO [Products]
    ([ProductName], [Description], [UnitPrice], [Quantity], [SellerID], [ProductStatus], [ImagePath])
SELECT [Name], [Description], [Price], [Stock], @sellerId, [Status], [Image]
FROM (VALUES
    (N'PlayStation 5 Pro', N'Home console with a DualSense controller.', 3499.00, 4, N'Active', N'/images/demo/ps5-pro.webp'),
    (N'ROG Ally X', N'Windows handheld with 1 TB of storage.', 3299.00, 2, N'Active', N'/images/demo/rog-ally-x.jpg'),
    (N'Steam Deck', N'Handheld gaming PC for your Steam library.', 2499.00, 8, N'Active', N'/images/demo/steam-deck.webp'),
    (N'Demo Laptop', N'Sample listing awaiting Admin review.', 2799.00, 5, N'Pending', NULL)
) AS [Seed] ([Name], [Description], [Price], [Stock], [Status], [Image])
WHERE NOT EXISTS (
    SELECT 1 FROM [Products]
    WHERE [SellerID] = @sellerId AND [ProductName] = [Seed].[Name]
);

IF NOT EXISTS (SELECT 1 FROM [Promotions] WHERE [PromotionCode] = N'DEMO10')
BEGIN
    INSERT INTO [Promotions] ([PromotionCode], [PromotionName], [Description], [DiscountPercentage], [StartDate], [PromotionStatus])
    VALUES (N'DEMO10', N'Demo promotion', N'10% off a sample order.', 10, DATEADD(day, -1, GETDATE()), N'Active');
END;
" >/dev/null

cat <<EOF

Eduvo demo is ready:
  Application: http://localhost:$app_port
  Admin:       http://localhost:$app_port/Admin/Login
  Seller:      http://localhost:$app_port/Seller/Login
  Shopper:     http://localhost:$app_port/User/Login
  Mailpit:     http://localhost:$mailpit_port

Disposable login:
  Admin:   demo-admin / Admin123!
  Seller:  demo-seller / Seller123!
  Shopper: demo-shopper / Shopper123!
  Shop mail: demo-shopper@example.test

Keep this terminal open while recording. Ctrl+C stops the app and the
disposable containers started for this run.
EOF

env "ConnectionStrings__JQuery-ECommerceAss=${sql_connection}" \
    dotnet run --no-launch-profile --project JQuery-ECommerceAss.csproj
