#!/bin/bash
# Builds Chronokeep.app and puts it in a .dmg, signing and notarizing both when the details are set.
#
# Usage: packaging/macos/build-dmg.sh VERSION PUBLISH_DIR OUT_DIR
#   VERSION      the version to package, a leading "v" is fine (v2.0.0)
#   PUBLISH_DIR  the output of: dotnet publish -c Release -r osx-x64 --self-contained
#   OUT_DIR      where to put the .dmg
#
# Signing (optional): set MACOS_SIGNING_IDENTITY to a "Developer ID Application: ..." certificate
# that's in the keychain. Without it the app and .dmg are unsigned.
#
# Notarizing (optional, needs signing): set NOTARY_KEY_FILE (an App Store Connect API key .p8),
# NOTARY_KEY_ID and NOTARY_ISSUER_ID.
#
# Run it on a Mac, from the repository root.
set -euo pipefail

if [ $# -ne 3 ]; then
    echo "Usage: $0 VERSION PUBLISH_DIR OUT_DIR" >&2
    exit 1
fi

VERSION="${1#v}"
PUBLISH_DIR="$2"
OUT_DIR="$3"
# CFBundleShortVersionString only allows numbers and dots, so drop anything like -3-gabc123.
BUNDLE_VERSION="${VERSION%%-*}"
SIGNING_IDENTITY="${MACOS_SIGNING_IDENTITY:-}"

if [ ! -x "$PUBLISH_DIR/Chronokeep" ]; then
    echo "No Chronokeep executable in $PUBLISH_DIR, run dotnet publish for osx-x64 first." >&2
    exit 1
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT
APP="$WORK_DIR/dmg/Chronokeep.app"

echo "---- Building Chronokeep.app $BUNDLE_VERSION ----"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
# ditto keeps file permissions, like the executable bit.
ditto "$PUBLISH_DIR" "$APP/Contents/MacOS"
sed "s/__VERSION__/$BUNDLE_VERSION/g" packaging/macos/Info.plist > "$APP/Contents/Info.plist"

# Make the app icon from the 512x512 PNG.
ICONSET="$WORK_DIR/Chronokeep.iconset"
mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do
    sips -z $size $size Chronokeep/help/icons/icon.png --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
    double=$((size * 2))
    if [ $double -le 512 ]; then
        sips -z $double $double Chronokeep/help/icons/icon.png --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
    fi
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/Chronokeep.icns"

if [ -n "$SIGNING_IDENTITY" ]; then
    echo "---- Signing Chronokeep.app ----"
    # Notarizing needs every executable file signed, not only the app. Sign each one (native libraries,
    # and .NET's own tools like createdump) first, then the app, which signs the main executable.
    find "$APP/Contents/MacOS" -type f ! -name Chronokeep -print0 | while IFS= read -r -d '' binary; do
        if file -b "$binary" | grep -q "Mach-O"; then
            codesign --force --timestamp --options runtime \
                --entitlements packaging/macos/entitlements.plist \
                --sign "$SIGNING_IDENTITY" "$binary"
        fi
    done
    codesign --force --timestamp --options runtime \
        --entitlements packaging/macos/entitlements.plist \
        --sign "$SIGNING_IDENTITY" "$APP"
    codesign --verify --strict --verbose=2 "$APP"
else
    echo "MACOS_SIGNING_IDENTITY isn't set, the app won't be signed."
fi

echo "---- Building the .dmg ----"
# A link to Applications so people can drag the app straight in.
ln -s /Applications "$WORK_DIR/dmg/Applications"
mkdir -p "$OUT_DIR"
DMG="$OUT_DIR/Chronokeep-$VERSION-x64.dmg"
rm -f "$DMG"
hdiutil create -volname "Chronokeep" -srcfolder "$WORK_DIR/dmg" -ov -format UDZO "$DMG"

if [ -n "$SIGNING_IDENTITY" ]; then
    codesign --force --timestamp --sign "$SIGNING_IDENTITY" "$DMG"
    if [ -n "${NOTARY_KEY_FILE:-}" ] && [ -n "${NOTARY_KEY_ID:-}" ] && [ -n "${NOTARY_ISSUER_ID:-}" ]; then
        echo "---- Notarizing ----"
        xcrun notarytool submit "$DMG" \
            --key "$NOTARY_KEY_FILE" --key-id "$NOTARY_KEY_ID" --issuer "$NOTARY_ISSUER_ID" \
            --wait
        # Attach the notarization so it can be checked without a network connection.
        xcrun stapler staple "$DMG"
    else
        echo "Notarizing details aren't set, the .dmg won't be notarized."
    fi
fi

ls -l "$OUT_DIR"
