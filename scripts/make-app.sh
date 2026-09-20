#!/bin/bash
set -euo pipefail
swift build -c release
APP="build/MonitorOff.app/Contents"
mkdir -p "$APP/MacOS" "$APP/Resources"
cp ".build/release/MonitorOff" "$APP/MacOS/MonitorOff"
cp "Resources/Info.plist" "$APP/Info.plist"
chmod +x scripts/make-app.sh
echo "Built build/MonitorOff.app"
