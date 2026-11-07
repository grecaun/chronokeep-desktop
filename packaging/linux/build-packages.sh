#!/bin/bash
# Builds the Linux .deb and .rpm packages.
#
# Usage: packaging/linux/build-packages.sh VERSION PUBLISH_DIR OUT_DIR
#   VERSION      the version to package, a leading "v" is fine (v2.0.0)
#   PUBLISH_DIR  the output of: dotnet publish -c Release -r linux-x64 --self-contained
#   OUT_DIR      where to put the packages
#
# Signing: set GPG_KEY_FILE to an armored GPG private key (and NFPM_PASSPHRASE if it has a passphrase)
# to sign both packages. Without it the packages are built unsigned.
#
# Needs nfpm on the PATH. Run it from the repository root.
set -euo pipefail

if [ $# -ne 3 ]; then
    echo "Usage: $0 VERSION PUBLISH_DIR OUT_DIR" >&2
    exit 1
fi

export VERSION="${1#v}"
export PUBLISH_DIR="$2"
OUT_DIR="$3"
export GPG_KEY_FILE="${GPG_KEY_FILE:-}"

if [ ! -x "$PUBLISH_DIR/Chronokeep" ]; then
    echo "No Chronokeep executable in $PUBLISH_DIR, run dotnet publish for linux-x64 first." >&2
    exit 1
fi

if [ -z "$GPG_KEY_FILE" ]; then
    echo "GPG_KEY_FILE isn't set, the packages won't be signed."
fi

mkdir -p "$OUT_DIR"
# nfpm doesn't fill in environment variables everywhere (like file paths), so fill them in here.
CONFIG="$(mktemp)"
trap 'rm -f "$CONFIG"' EXIT
sed -e "s|\${VERSION}|$VERSION|g" \
    -e "s|\${PUBLISH_DIR}|$PUBLISH_DIR|g" \
    -e "s|\${GPG_KEY_FILE}|$GPG_KEY_FILE|g" \
    packaging/linux/nfpm.yaml > "$CONFIG"
nfpm package --config "$CONFIG" --packager deb --target "$OUT_DIR/"
nfpm package --config "$CONFIG" --packager rpm --target "$OUT_DIR/"
ls -l "$OUT_DIR"
