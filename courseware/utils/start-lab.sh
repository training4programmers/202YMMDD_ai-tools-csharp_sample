#!/usr/bin/env bash
# start-lab.sh: copy a demo or activity starter to a lab folder OUTSIDE the course repo and make it a
# git repo. (macOS/Linux/Git Bash twin of start-lab.ps1; see that file for the full explanation.)
#
# AI coding tools read instruction files from the folders around your code (Claude Code loads every
# CLAUDE.md up to the root, and AGENTS.md when there is no CLAUDE.md; VS Code reads AGENTS.md; Visual
# Studio reads .github/copilot-instructions.md at the git repo root). The course repo has its own
# instruction files for maintaining the courseware, so labs run in a copy under ~/labs instead, with a
# baseline commit so `git diff` shows exactly what changed.
#
# Usage:
#   ./start-lab.sh activity 12            # activities/12-*/begin  -> ~/labs/activity-12-<slug>
#   ./start-lab.sh activity 3 --bonus     # activities/bonus/03-*  -> ~/labs/bonus-activity-03-<slug>
#   ./start-lab.sh demo 14                # demos/14-*/begin       -> ~/labs/demo-14-<slug>
#   ./start-lab.sh activity 12 --solution # copy solution/ instead (to compare)
#   ./start-lab.sh activity 12 --force    # replace an existing lab folder
#   LABS_ROOT=/tmp/labs ./start-lab.sh activity 1
set -euo pipefail

usage() { sed -n '2,17p' "$0" | sed 's/^# \{0,1\}//'; exit 1; }

[[ $# -ge 2 ]] || usage
kind="$1"; number="$2"; shift 2
case "$kind" in activity|demo) ;; *) usage ;; esac
[[ "$number" =~ ^[0-9]+$ ]] || usage

bonus=0; solution=0; force=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --bonus) bonus=1 ;;
    --solution) solution=1 ;;
    --force) force=1 ;;
    *) usage ;;
  esac
  shift
done

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
courseware="$(dirname "$script_dir")"
repo_root="$(dirname "$courseware")"

tier="$courseware/$([[ $kind == demo ]] && echo demos || echo activities)"
[[ $bonus -eq 1 ]] && tier="$tier/bonus"

nn="$(printf '%02d' "$((10#$number))")"
shopt -s nullglob
matches=("$tier/$nn"-*/)
shopt -u nullglob
if [[ ${#matches[@]} -ne 1 ]]; then
  echo "Could not find exactly one folder matching '$nn-*' in $tier." >&2
  exit 1
fi
pair="${matches[0]%/}"
pair_name="$(basename "$pair")"

source_name=begin
[[ $solution -eq 1 ]] && source_name=solution
source="$pair/$source_name"
[[ -d "$source" ]] || { echo "$pair_name has no $source_name/ folder." >&2; exit 1; }

labs_root="${LABS_ROOT:-$HOME/labs}"
mkdir -p "$labs_root"
labs_root="$(cd "$labs_root" && pwd)"
case "$labs_root/" in
  "$repo_root"/*) echo "LABS_ROOT ($labs_root) is inside the course repo. Choose a folder outside it." >&2; exit 1 ;;
esac

prefix="$kind"
[[ $bonus -eq 1 ]] && prefix="bonus-$kind"
lab_name="$prefix-$pair_name"
[[ $solution -eq 1 ]] && lab_name="$lab_name-solution"
destination="$labs_root/$lab_name"

# Build output nests deep, and some native libraries (SQLite) fail to load from very long paths on Windows.
if [[ ${#destination} -gt 120 ]]; then
  echo "warning: lab path is ${#destination} characters long; if tests fail with 'filename or extension is too long', set a shorter LABS_ROOT such as /c/labs." >&2
fi

if [[ -e "$destination" ]]; then
  if [[ $force -ne 1 ]]; then
    echo "$destination already exists. Re-run with --force to replace it (your changes there will be lost)." >&2
    exit 1
  fi
  rm -rf "$destination"
fi

mkdir -p "$destination"
# Copy everything except build output and IDE state.
(cd "$source" && tar --exclude='bin' --exclude='obj' --exclude='.vs' --exclude='packages' \
  --exclude='TestResults' -cf - .) | (cd "$destination" && tar -xf -)

cd "$destination"
git init --quiet --initial-branch=main ||
  { echo "git init failed in $destination (see git's message above). Is Git 2.28 or later installed?" >&2; exit 1; }
git -c core.safecrlf=false add --all ||
  { echo "git add failed in $destination (see git's message above)." >&2; exit 1; }
identity=()
git config user.email >/dev/null || identity+=(-c user.email=lab@example.invalid)
git config user.name >/dev/null || identity+=(-c user.name="Course Lab")
# The baseline is a local snapshot, not your work, so it is never signed (no GPG/SSH key prompt).
message="Baseline: $pair_name ($source_name)"
if ! git ${identity[@]+"${identity[@]}"} -c commit.gpgsign=false commit --quiet --message "$message"; then
  retry="git"
  for arg in ${identity[@]+"${identity[@]}"}; do
    [[ "$arg" == *" "* ]] && retry+=" \"$arg\"" || retry+=" $arg"
  done
  retry+=" -c commit.gpgsign=false commit -m \"$message\""
  cat >&2 <<EOF

The lab files were copied to $destination, but the baseline commit failed (see git's message above),
so 'git diff' has nothing to compare against yet.

The usual cause is a commit hook, such as a company secret scanner set with a global core.hooksPath.
Some labs plant FAKE secrets on purpose (Activity 24 ships a made-up connection string with a password,
Demo 24 a made-up API key; finding them is the lesson), and a scanner rightly flags them. Check with
your instructor, then make the baseline commit yourself in a way your organization's policy allows:

    cd "$destination"
    $retry

To start over instead, re-run this script with --force.
EOF
  exit 1
fi

cat <<EOF

Lab ready: $destination
  cd "$destination"
  dotnet build            # confirm the starter builds
  git diff                # review what you and your assistant changed
  git restore . && git clean -fd   # throw away changes and return to the baseline

EOF
