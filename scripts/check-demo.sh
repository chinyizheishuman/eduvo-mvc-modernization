#!/usr/bin/env bash
set -euo pipefail

if (( $# > 2 )); then
    echo "Usage: $0 [app-port] [mailpit-port]" >&2
    exit 2
fi
app_port="${1:-5067}"
mailpit_port="${2:-8025}"
for port in "$app_port" "$mailpit_port"; do
    if [[ ! "$port" =~ ^[1-9][0-9]{0,4}$ ]] || (( port > 65535 )); then
        echo "Usage: $0 [app-port] [mailpit-port]" >&2
        exit 2
    fi
done

app="http://127.0.0.1:$app_port"
request() {
    curl --noproxy '*' --silent --show-error --max-time 5 --output /dev/null --write-out "$1" "$2"
}

expect_ok() {
    local label="$1" url="$2" status
    status=$(request '%{http_code}' "$url") || exit 1
    if [[ "$status" != 200 ]]; then
        echo "$label returned HTTP $status (expected 200)." >&2
        exit 1
    fi
    echo "$label responds."
}

expect_role_redirect() {
    local role="$1" route="$2" response login
    response=$(request '%{http_code} %{redirect_url}' "$app$route") || exit 1
    login="302 $app/$role/Login"
    if [[ "$response" != "$login" && "$response" != "$login"\?* ]]; then
        echo "$route did not redirect a signed-out visitor to /$role/Login." >&2
        exit 1
    fi
    echo "$route redirects to its role's sign-in page."
}

for role in Admin Seller User; do
    expect_ok "$role sign-in" "$app/$role/Login"
done
expect_role_redirect Admin /Admin/Index
expect_role_redirect Seller /Seller/ManageProducts
expect_role_redirect User /User/Products
expect_ok "Shared stylesheet" "$app/css/geek-lab.css"
expect_ok "Mailpit" "http://127.0.0.1:$mailpit_port/api/v1/info"
