#!/bin/bash
# PreToolUse/Edit|Write guard — block writes to files likely containing secrets.

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

[ -z "$FILE_PATH" ] && exit 0

BASENAME=$(basename "$FILE_PATH")

if echo "$BASENAME" | grep -qiE '^\.env$|^\.env\.|credentials|secret|\.pem$|\.key$|\.p12$|\.pfx$|\.jks$|id_rsa|id_ed25519|token\.json|service.account\.json'; then
  echo "Blocked: $BASENAME is a sensitive file." >&2
  echo "Do not write to secret files directly. Use a secrets manager or environment variables." >&2
  exit 2
fi

exit 0
