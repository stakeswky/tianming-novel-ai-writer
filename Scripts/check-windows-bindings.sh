#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if ! command -v rg >/dev/null 2>&1; then
  echo "error: rg is required to scan Windows-specific bindings" >&2
  exit 2
fi

PATTERN='net8\.0-windows|UseWPF|WindowsDesktop|System\.Windows|Microsoft\.Win32|Registry\.|RegistryKey|RegistryValueKind|OpenSubKey|CreateSubKey|DeleteSubKeyTree|DllImport|user32\.dll|kernel32\.dll|ntdll\.dll|wininet\.dll|dwmapi\.dll|TMProtect\.dll|System\.Management|System\.Speech|NAudio|WebView2\.Wpf|Microsoft\.Web\.WebView2|Microsoft\.Toolkit\.Uwp\.Notifications|Windows\.Forms|ProtectedData|Emoji\.Wpf|DiffPlex\.Wpf|Markdig\.Wpf'

echo "Scanning Windows-specific bindings under Core/, Framework/, Modules/, Services/..."
echo

rg -n --glob '*.cs' --glob '*.xaml' --glob '*.csproj' --glob '*.props' "$PATTERN" \
  Core Framework Modules Services || true

echo
echo "Summary by file:"
rg -l --glob '*.cs' --glob '*.xaml' --glob '*.csproj' --glob '*.props' "$PATTERN" \
  Core Framework Modules Services | sort | while IFS= read -r file; do
    count="$(rg -n --glob '*.cs' --glob '*.xaml' --glob '*.csproj' --glob '*.props' "$PATTERN" "$file" | wc -l | tr -d ' ')"
    printf "%5s  %s\n" "$count" "$file"
  done
