#!/usr/bin/env bash
# Squares up a map icon: cuts away the transparent padding around it, scales what is left so its
# longer side fills the canvas, and centres it on a transparent square. Every icon then touches at
# least two opposite edges, so pins drawn at the same size in game look the same size.
# Takes images of any size; the source can be a full size render.
#
#   -o, --out FILE      write here instead of over the input (one input only)
#   -s, --size N        edge of the square output in pixels (default 64)
#   -t, --threshold N   alpha, 0-255, a pixel needs to count as part of the icon (default 8),
#                       so a faint glow or antialiasing noise does not keep padding alive
#
# Usage: scripts/icon.sh [-s N] [-t N] [-o out.png] image.png [image.png ...]
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

size=64
threshold=8
out=
images=()

while [ $# -gt 0 ]; do
    case "$1" in
        -o|--out)       out=${2:?"$1 needs a file"}; shift 2 ;;
        -s|--size)      size=${2:?"$1 needs a number"}; shift 2 ;;
        -t|--threshold) threshold=${2:?"$1 needs a number"}; shift 2 ;;
        -h|--help)      usage; exit 0 ;;
        -*)             die "unknown argument: $1" ;;
        *)              images+=("$1"); shift ;;
    esac
done

[ ${#images[@]} -gt 0 ] || { usage; exit 1; }
[ -z "$out" ] || [ ${#images[@]} -eq 1 ] || die '--out takes a single input image'
[[ $size =~ ^[1-9][0-9]*$ ]] || die "--size must be a positive number, got '$size'"
[[ $threshold =~ ^[0-9]+$ ]] && [ "$threshold" -le 255 ] ||
    die "--threshold must be 0-255, got '$threshold'"
command -v magick >/dev/null || die 'ImageMagick 7 (magick) not found - install imagemagick.'

for img in "${images[@]}"; do
    [ -f "$img" ] || die "no such file: $img"
    dest=${out:-$img}

    # The bounding box of every pixel above the threshold, as WxH+X+Y. Measured on a thresholded
    # copy of the alpha channel so the crop below still keeps the soft edges inside the box.
    box=$(magick "$img" -alpha extract -threshold "$(( threshold * 100 / 255 ))%" \
        -format '%@' info:)
    case "$box" in
        0x0*|'') die "$img: nothing above alpha $threshold - is it transparent at all?" ;;
    esac
    before=$(magick identify -format '%wx%h' "$img")

    tmp=$(mktemp --suffix=.png)
    magick "$img" -crop "$box" +repage \
        -resize "${size}x${size}" \
        -background none -gravity center -extent "${size}x${size}" \
        -define png:color-type=6 "PNG32:$tmp"
    mv "$tmp" "$dest"
    chmod 644 "$dest"

    info "$img ($before, icon $box) -> $dest (${size}x${size}, icon $(magick "$dest" -format '%@' info:))"
done
