# Claude Custom Subagents

This directory contains configuration files for custom Claude subagents used in the Galaxy Scavengers project.

## Available Agents

### 1. `unity-coder`

- **Purpose**: Expert Unity C# developer. Writes, edits, and refactors high-performance Unity scripts.
- **Key Rules**: Bypassing null checks gotchas (`?.`/`??`), using `CoroutineCache`, caching `GetComponent`.
- **Tools**: Read, Grep, Glob, Edit, Write.

### 2. `unity-perf-auditor` (Read-Only)

- **Purpose**: Scans your code for performance bottlenecks, redundant GC allocations, and unsafe Unity C# patterns.
- **Tools**: Read, Grep, Glob.

### 3. `unity-audio-fmod`

- **Purpose**: Specialist in FMOD sound integration, audio manager scripts, and UI audio events.
- **Tools**: Read, Grep, Glob, Edit, Write.

### 4. `claude-md-author`

- **Purpose**: Maintainer of markdown convention files (`CLAUDE.md`, agent, and skill files).
- **Tools**: Read, Grep, Glob, Bash, Edit, Write.

### 5. `claude-md-linter` (Read-Only)

- **Purpose**: Linter for markdown guidelines, checking line counts, structure, and tone.
- **Tools**: Read, Grep, Glob, Bash.
