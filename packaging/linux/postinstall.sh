#!/bin/sh
# Runs as root after the .deb or .rpm is installed or upgraded.
set -e

# Chronokeep keeps its database, logs and exports here, shared by every user on the computer
# (see Helpers/ProgramFolders.cs). Only root can create it, which is why the package does.
# No sticky bit, so any user can clean up the temporary files SQLite leaves if another user's
# session stops in the middle of saving.
mkdir -p /var/lib/chronokeep
chmod 0777 /var/lib/chronokeep

# The folder is left in place when the package is removed so nobody loses their race data.

# Refresh the application menu and icons if the tools are installed.
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database -q /usr/share/applications || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true
fi
