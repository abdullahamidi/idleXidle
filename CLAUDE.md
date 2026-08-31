# Claude Code Game Studios -- Game Studio Agent Architecture

Indie game development managed through 49 coordinated Claude Code subagents.
Each agent owns a specific domain, enforcing separation of concerns and quality.

## Technology Stack

- **Engine**: MonoGame 3.8.4.1
- **Language**: C# (.NET 8+)
- **Version Control**: Git with trunk-based development
- **Build System**: .NET SDK (dotnet CLI / MSBuild) + MonoGame Content Builder (MGCB)
- **Asset Pipeline**: MonoGame Content Pipeline (MGCB Editor)

> **Note**: This template's dedicated engine-specialist agents (Godot/Unity/
> Unreal sub-specialists) do not cover MonoGame. Specialist routing falls
> back to general programmer agents — see `.claude/docs/technical-preferences.md`
> Engine Specialists section for the routing table.

## Project Structure

@.claude/docs/directory-structure.md

## Engine Version Reference

@docs/engine-reference/monogame/VERSION.md

## Technical Preferences

@.claude/docs/technical-preferences.md

## Coordination Rules

@.claude/docs/coordination-rules.md

## Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for full protocol and examples.

> **First session?** If the project has no engine configured and no game concept,
> run `/start` to begin the guided onboarding flow.

## Coding Standards

@.claude/docs/coding-standards.md

## Context Management

@.claude/docs/context-management.md
