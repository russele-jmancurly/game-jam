#!/usr/bin/env bash
# Runs a Python script inside headless Blender (no UI, factory settings, non-zero exit on Python errors).
#
#   tools/blender/blender.sh <script.py> [script args...]
#   tools/blender/blender.sh tools/blender/scripts/inspect.py Assets/GameJam/Art/Props/Food/Burger_Visual.fbx
#
# Blender is found via $BLENDER_EXE, then `blender` on PATH, then the newest install under
# "C:/Program Files/Blender Foundation", then the newest install Windows lists under installed programs (any drive or
# folder, Steam included). Script arguments go after Blender's `--` separator.
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "usage: $0 <script.py> [args...]" >&2
  exit 2
fi

find_blender() {
  if [[ -n "${BLENDER_EXE:-}" && -x "${BLENDER_EXE}" ]]; then
    echo "${BLENDER_EXE}"
    return
  fi
  if command -v blender >/dev/null 2>&1; then
    command -v blender
    return
  fi
  local root="/c/Program Files/Blender Foundation"
  local newest
  newest=$(ls -1d "$root"/Blender* 2>/dev/null | sort -V | tail -1 || true)
  if [[ -n "$newest" && -x "$newest/blender.exe" ]]; then
    echo "$newest/blender.exe"
    return
  fi
  # Installed somewhere else (another drive, a custom folder, Steam): Windows' installed-programs list knows where.
  # Newest version first.
  if command -v powershell.exe >/dev/null 2>&1; then
    local found
    found=$(powershell.exe -NoProfile -NonInteractive -Command '
      $keys = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
              "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
              "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*"
      Get-ItemProperty $keys -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -like "Blender*" -and $_.InstallLocation } |
        Sort-Object { try { [version]$_.DisplayVersion } catch { [version]"0.0" } } -Descending |
        ForEach-Object { Join-Path $_.InstallLocation "blender.exe" } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1' 2>/dev/null | tr -d '\r' || true)
    if [[ -n "$found" ]]; then
      cygpath -u "$found" 2>/dev/null || echo "$found"
      return
    fi
  fi
  echo "Blender not found. Install it, or set BLENDER_EXE to blender.exe." >&2
  exit 4
}

BLENDER="$(find_blender)"
SCRIPT="$1"
shift

exec "$BLENDER" --background --factory-startup --python-exit-code 1 --python "$SCRIPT" -- "$@"
