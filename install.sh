#!/usr/bin/env sh
# Installs scdl on Linux and macOS: downloads the release archive for this
# machine, checks it against the published SHA256SUMS.txt, unpacks it and says
# what to do about PATH.
#
# POSIX sh rather than bash, and no jq: whoever runs this has installed nothing
# yet, and the point is that it works on a bare machine.
#
#   curl -sSL https://raw.githubusercontent.com/danganhphu/scdl-4life/main/install.sh | sh
#
# Environment:
#   SCDL_VERSION      release to install, such as 0.2.0. Defaults to the latest.
#   SCDL_INSTALL_DIR  where to unpack. Defaults to ~/.local/bin.

set -eu

repository='danganhphu/scdl-4life'
install_dir="${SCDL_INSTALL_DIR:-$HOME/.local/bin}"

step() {
    printf '==> %s\n' "$1"
}

die() {
    printf 'error: %s\n' "$1" >&2
    exit 1
}

fetch() {
    # curl on macOS, wget on the minimal Linux images that ship without it.
    if command -v curl >/dev/null 2>&1; then
        curl -fsSL "$1" -o "$2"
    elif command -v wget >/dev/null 2>&1; then
        wget -qO "$2" "$1"
    else
        die 'neither curl nor wget is installed'
    fi
}

detect_runtime() {
    os="$(uname -s)"
    arch="$(uname -m)"

    case "$os/$arch" in
        Linux/x86_64) echo 'linux-x64' ;;
        Darwin/arm64) echo 'osx-arm64' ;;
        Darwin/x86_64) echo 'osx-x64' ;;
        *) die "no scdl build for $os $arch. Build from source: https://github.com/$repository" ;;
    esac
}

verify() {
    archive="$1"
    sums="$2"
    name="$(basename "$archive")"

    expected="$(grep " [ *]\{0,1\}${name}\$" "$sums" | awk '{ print $1 }')"
    [ -n "$expected" ] || die "SHA256SUMS.txt has no entry for $name"

    # sha256sum is coreutils, shasum is what macOS ships.
    if command -v sha256sum >/dev/null 2>&1; then
        actual="$(sha256sum "$archive" | awk '{ print $1 }')"
    elif command -v shasum >/dev/null 2>&1; then
        actual="$(shasum -a 256 "$archive" | awk '{ print $1 }')"
    else
        die 'neither sha256sum nor shasum is installed'
    fi

    [ "$actual" = "$expected" ] ||
        die "checksum mismatch for $name. Expected $expected, got $actual. Do not run it."
}

runtime="$(detect_runtime)"
version="${SCDL_VERSION:-}"

if [ -z "$version" ]; then
    step 'Finding the latest release'
    staging_probe="$(mktemp)"
    fetch "https://api.github.com/repos/$repository/releases/latest" "$staging_probe"
    version="$(sed -n 's/.*"tag_name"[[:space:]]*:[[:space:]]*"v\{0,1\}\([^"]*\)".*/\1/p' "$staging_probe")"
    rm -f "$staging_probe"
    [ -n "$version" ] || die 'could not read the latest version from the GitHub API'
fi

archive_name="scdl-$version-$runtime.tar.gz"
base="https://github.com/$repository/releases/download/v$version"
staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT

step "Downloading scdl $version for $runtime"
fetch "$base/$archive_name" "$staging/$archive_name"
fetch "$base/SHA256SUMS.txt" "$staging/SHA256SUMS.txt"

step 'Verifying the checksum'
verify "$staging/$archive_name" "$staging/SHA256SUMS.txt"

step "Unpacking into $install_dir"
mkdir -p "$install_dir"
tar -xzf "$staging/$archive_name" -C "$staging"
mv -f "$staging/scdl" "$install_dir/scdl"
chmod +x "$install_dir/scdl"

# macOS quarantines anything a browser downloaded. curl does not set the flag,
# but clearing it costs nothing and covers a manual download.
if [ "$(uname -s)" = 'Darwin' ] && command -v xattr >/dev/null 2>&1; then
    xattr -d com.apple.quarantine "$install_dir/scdl" 2>/dev/null || true
fi

printf '\n  scdl %s is at %s/scdl\n' "$version" "$install_dir"

case ":$PATH:" in
    *":$install_dir:"*) ;;
    *) printf '  %s is not on your PATH. Add it in your shell profile:\n\n    export PATH="%s:$PATH"\n' \
        "$install_dir" "$install_dir" ;;
esac

printf '  Try: scdl formats "https://soundcloud.com/<user>/<track>"\n\n'
