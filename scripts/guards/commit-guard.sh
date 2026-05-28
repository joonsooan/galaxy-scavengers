#!/bin/bash
# PreToolUse/Bash guard — block git commits containing AI/Claude/Anthropic mentions.

INPUT=$(cat)
CMD=$(echo "$INPUT" | jq -r '.tool_input.command // empty')

# Only check git commit commands
echo "$CMD" | grep -qE 'git\s+commit' || exit 0

# Extract commit message (between heredoc markers or after -m)
MSG=$(echo "$CMD" | sed -n "s/.*-m ['\"]\\(.*\\)['\"].*/\\1/p")
[ -z "$MSG" ] && MSG="$CMD"

# Block forbidden AI/attribution mentions (excludes filenames like CLAUDE.md)
if echo "$MSG" | grep -qE 'Co-[Aa]uthored-[Bb]y|[Aa]nthrop(ic)?|\bClaude\b|\bAI\b'; then
  echo "Blocked: commit message contains AI/Claude/Anthropic attribution." >&2
  echo "Remove Co-Authored-By, Claude, AI, or Anthropic mentions before committing." >&2
  exit 2
fi

exit 0
