#!/usr/bin/env bash
# Run from any directory. On Windows, use Git Bash.
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

if ! git lfs version >/dev/null 2>&1; then
    echo "Install Git LFS from https://git-lfs.com, then run this script again." >&2
    exit 1
fi
git lfs install --local

unity_version="$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt | tr -d '\r')"
merge_tool="${1:-}"
if [[ -z "$merge_tool" ]]; then
    for candidate in \
        "/Applications/Unity/Hub/Editor/$unity_version/Unity.app/Contents/Helpers/UnityYAMLMerge" \
        "/Applications/Unity/Hub/Editor/$unity_version/Unity.app/Contents/Tools/UnityYAMLMerge" \
        "/c/Program Files/Unity/Hub/Editor/$unity_version/Editor/Data/Tools/UnityYAMLMerge.exe" \
        "$HOME/Unity/Hub/Editor/$unity_version/Editor/Data/Tools/UnityYAMLMerge"; do
        if [[ -x "$candidate" ]]; then
            merge_tool="$candidate"
            break
        fi
    done
fi

if [[ -z "$merge_tool" || ! -x "$merge_tool" ]]; then
    echo "Git LFS is ready. To enable Smart Merge, rerun with the full path to UnityYAMLMerge." >&2
    exit 1
fi
# Escape characters significant inside Git's shell command, including spaces.
printf -v quoted_tool '%q' "$merge_tool"
git config --local merge.unityyamlmerge.name "Unity Smart Merge"
git config --local merge.unityyamlmerge.driver "$quoted_tool merge -p %O %B %A %A"
git config --local merge.unityyamlmerge.recursive binary
git config --local mergetool.unityyamlmerge.cmd "$quoted_tool merge -p \"\$BASE\" \"\$REMOTE\" \"\$LOCAL\" \"\$MERGED\""
git config --local mergetool.unityyamlmerge.trustExitCode false
echo "Git LFS and Unity Smart Merge are configured for this clone."
