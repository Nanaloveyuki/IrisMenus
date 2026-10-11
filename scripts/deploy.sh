#!/usr/bin/env bash
# Build and deploy IrisMenus; reuse the existing shader bundle, never ship dependencies.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Release}"
rimworld="${RIMWORLD_DIR:-${RimWorldDir:-/mnt/e/Apps/Steam/steamapps/common/RimWorld}}"
target="$rimworld/Mods/IrisMenus"
build_only=0
if [[ $# -gt 0 ]]; then
  if [[ $# -ne 1 || "$1" != "--build-only" ]]; then
    echo "Usage: $0 [--build-only]" >&2
    exit 1
  fi
  build_only=1
fi
if [[ "$configuration" != "Release" && "$configuration" != "Debug" ]]; then
  echo "CONFIGURATION must be Release or Debug." >&2
  exit 1
fi

managed="$rimworld/RimWorldWin64_Data/Managed"
if [[ ! -f "$managed/Assembly-CSharp.dll" ]]; then
  echo "RimWorld managed assemblies not found: $managed" >&2
  exit 1
fi

check_game_stopped() {
  if command -v powershell.exe >/dev/null 2>&1; then
    local count
    count="$(powershell.exe -NoProfile -Command '$ErrorActionPreference = "Stop"; @(Get-Process -Name RimWorldWin64,RimWorldWin64Steam,RimWorldWin,RimWorld -ErrorAction SilentlyContinue).Count' | tr -d '\r')"
    if [[ "$count" != "0" ]]; then
      echo "Exit RimWorld before deploying." >&2
      exit 1
    fi
  elif pgrep -x 'RimWorld|RimWorldLinux|RimWorldWin64' >/dev/null; then
    echo "Exit RimWorld before deploying." >&2
    exit 1
  fi
}

if [[ "$build_only" == 0 ]]; then
  check_game_stopped
fi

dotnet build "$root/Source/IrisMenus.csproj" -c "$configuration" -p:RimWorldManagedDir="$managed" --nologo
if [[ "$build_only" == 1 ]]; then
  exit 0
fi
check_game_stopped

python3 - "$root" "$target" <<'PY'
import hashlib
import shutil
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(sys.argv[1]).resolve()
target = Path(sys.argv[2])
package_id = "Nanaloveyuki.IrisMenus"
folders = ("1.6/Defs", "1.6/Languages", "docs")
files = (
    "About/About.xml", "README.md", "LICENSE", "guide.md", "guide_agents.md",
    "About/Preview.png", "About/ModIcon.png",
    "1.6/Assemblies/IrisMenus.dll", "1.6/AssetBundles/irismenus_frost",
)


def check_identity(about):
    if ET.parse(about).getroot().findtext("packageId") != package_id:
        raise SystemExit(f"Unexpected mod identity: {about}")


check_identity(root / "About/About.xml")
if target.is_symlink():
    raise SystemExit(f"Target is a symlink: {target}")
if target.exists():
    if not target.is_dir():
        raise SystemExit(f"Target is not a directory: {target}")
    about = target / "About/About.xml"
    if about.is_file():
        check_identity(about)
    elif any(target.iterdir()):
        raise SystemExit(f"Non-empty target has no mod identity: {target}")
if target.resolve() == root:
    raise SystemExit("Deployment target must not be the source repository.")

sources = [(root / file, Path(file)) for file in files]
for folder in folders:
    path = root / folder
    if not path.is_dir():
        raise SystemExit(f"Required source directory not found: {path}")
    sources.extend((item, item.relative_to(root)) for item in sorted(path.rglob("*")) if item.is_file())

# Validate the complete manifest before writing into the game directory.
for source, relative in sources:
    if not source.is_file():
        raise SystemExit(f"Required source file not found: {source}")
    destination = target / relative
    if destination.resolve() != target.resolve() / relative:
        raise SystemExit(f"Refusing to deploy through a symlink: {destination}")

for source, relative in sources:
    destination = target / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    if hashlib.sha256(source.read_bytes()).digest() != hashlib.sha256(destination.read_bytes()).digest():
        raise SystemExit(f"Hash mismatch: {relative}")

print(f"Deployed and SHA-256 verified {len(sources)} files: {target}")
PY
