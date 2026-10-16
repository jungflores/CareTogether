#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mobile_project="$project_root/src/CareTogether.Mobile/CareTogether.Mobile.csproj"
app_bundle="$project_root/src/CareTogether.Mobile/bin/Debug/net10.0-ios/iossimulator-arm64/CareTogether.Mobile.app"
bundle_id="com.caretogether.mobile"

if [[ -x /usr/local/share/dotnet/dotnet ]]; then
  dotnet_host=/usr/local/share/dotnet/dotnet
else
  dotnet_host="$(command -v dotnet)"
fi

cd "$project_root"

echo "Starting CareTogether backend services..."
docker compose up -d --build

echo "Waiting for the API..."
for attempt in {1..60}; do
  if curl --fail --silent --show-error http://localhost:8080/health >/dev/null 2>&1; then
    echo "API is ready at http://localhost:8080"
    break
  fi

  if [[ "$attempt" -eq 60 ]]; then
    echo "The API did not become ready. Recent API logs:" >&2
    docker compose logs --tail=80 api >&2
    exit 1
  fi

  sleep 2
done

if ! xcrun simctl list devices booted | grep -q '(Booted)'; then
  echo "No iOS Simulator is booted. Open Simulator, boot a device, and run this command again." >&2
  exit 1
fi

echo "Building the iOS app..."
"$dotnet_host" build "$mobile_project" \
  -f net10.0-ios \
  -p:RuntimeIdentifier=iossimulator-arm64

echo "Installing and launching CareTogether..."
xcrun simctl install booted "$app_bundle"
xcrun simctl launch booted "$bundle_id"

echo "CareTogether is running. Swagger is available at http://localhost:8080/swagger"
