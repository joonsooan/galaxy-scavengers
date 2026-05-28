#!/bin/bash
# PreToolUse Edit|Write guard for files under .claude/ that end with .md (or named CLAUDE.md).
# Catches common .claude/ convention drift at write time:
#   - Absolute paths — breaks portability across machines/CI.
#   - New agent missing required frontmatter (name/description/tools/model).

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
NEW_CONTENT=$(echo "$INPUT" | jq -r '.tool_input.content // .tool_input.new_string // empty')

# `tool_input.content` exists only on Write (full file). On Edit it's null.
IS_WHOLE_FILE=$(echo "$INPUT" | jq -r 'if .tool_input.content then "1" else "0" end')

# Only apply to .claude/**/*.md or any CLAUDE.md
case "$FILE_PATH" in
  */.claude/*.md|*/CLAUDE.md|*/.claude/*/*.md|*/.claude/*/*/*.md|*/.claude/*/*/*/*.md) ;;
  *) exit 0 ;;
esac

VIOLATIONS=()

# === 1. Absolute paths ===
if echo "$NEW_CONTENT" | grep -qE '/Users/[a-zA-Z0-9_-]+/'; then
  VIOLATIONS+=("Absolute path detected (/Users/...). Use project-relative paths (.claude/...) instead.")
fi

# === 2. Agent file frontmatter ===
# Only check on Write (whole file). On Edit we'd see only the changed chunk.
# Carve-out: agents/README.md and agents/_*.md are convention/docs files, not agents.
CHECK_FRONTMATTER=0
case "$FILE_PATH" in
  */.claude/agents/README.md|*/.claude/agents/_*.md|*/.claude/agents/*/_*.md) ;;
  */.claude/agents/*.md|*/.claude/agents/*/*.md)
    [ "$IS_WHOLE_FILE" = "1" ] && CHECK_FRONTMATTER=1
    ;;
esac

if [ "$CHECK_FRONTMATTER" = "1" ]; then
  HEAD=$(echo "$NEW_CONTENT" | head -20)
  for f in name description tools model; do
    if ! echo "$HEAD" | grep -qE "^${f}:"; then
      VIOLATIONS+=("Agent frontmatter missing '${f}:' field.")
    fi
  done
  if echo "$HEAD" | grep -qE '^description:.*\b(review|audit|check|lint|explore|validate)\b' \
     && ! echo "$HEAD" | grep -qE '^disallowedTools:.*Write.*Edit|^disallowedTools:.*Edit.*Write'; then
    VIOLATIONS+=("Read-only agent appears to be missing 'disallowedTools: Write, Edit'.")
  fi
fi

if [ ${#VIOLATIONS[@]} -gt 0 ]; then
  echo "Blocked: .claude/ convention violation:" >&2
  for v in "${VIOLATIONS[@]}"; do echo "  - $v" >&2; done
  echo "See .claude/agents/ for agent conventions." >&2
  exit 2
fi

exit 0
