#!/usr/bin/env bash
set -euo pipefail
WORKDIR="${1:-$(pwd)}"
if [ ! -f "$WORKDIR/package.json" ]; then
  echo "package.json not found in $WORKDIR" >&2
  exit 1
fi

echo "Running npm install in $WORKDIR..."
npm install --prefix "$WORKDIR"

echo "Installing Playwright browsers (chromium)..."
npx --yes --prefix "$WORKDIR" playwright install chromium

echo "Done." 
