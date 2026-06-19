# Claude Custom Subagents

This directory contains configuration files for custom Claude subagents used in the Galaxy Scavengers project.

## Available Agents

### 1. `unity-core-architect`
- **Purpose**: Specializes in global managers, singleton architecture, scene loading, and core systems (`Assets/Scripts/Core/`).
- **Tools**: Read, Grep, Glob, Edit, Write.

### 2. `unity-gameplay-coder`
- **Purpose**: Specializes in gameplay logic including unit behaviors, buildings, power, items, and resource grids (`Assets/Scripts/Gameplay/`).
- **Tools**: Read, Grep, Glob, Edit, Write.

### 3. `unity-gameplay-designer`
- **Purpose**: Specializes in balancing and designing the technology trees, unlocks, and stat upgrades using existing game elements.
- **Tools**: Read, Grep, Glob, Edit, Write.

### 4. `unity-ui-specialist`
- **Purpose**: Specializes in canvas UI rendering, panel controllers, dynamic UI list cells, and localization (`Assets/Scripts/UI/`).
- **Tools**: Read, Grep, Glob, Edit, Write.

### 5. `unity-perf-auditor` (Read-Only)
- **Purpose**: Scans code for performance bottlenecks, redundant GC allocations, and unsafe Unity C# patterns.
- **Tools**: Read, Grep, Glob.

### 6. `unity-audio-fmod`
- **Purpose**: Specialist in FMOD sound integration, audio manager scripts, and UI audio events.
- **Tools**: Read, Grep, Glob, Edit, Write.

### 7. `unity-coder` (Generalist)
- **Purpose**: General C# developer for script modification and utilities outside specialized domains.
- **Tools**: Read, Grep, Glob, Edit, Write.

### 8. `claude-md-author`
- **Purpose**: Maintainer of markdown convention files (`CLAUDE.md`, agent, and skill files).
- **Tools**: Read, Grep, Glob, Bash, Edit, Write.

### 9. `claude-md-linter` (Read-Only)
- **Purpose**: Linter for markdown guidelines, checking line counts, structure, and tone.
- **Tools**: Read, Grep, Glob, Bash.
