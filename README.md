# seed-skill

Seed project for a durable, AI-assisted web product.

The project is being defined from a Marco Zero foundation: decisions should be
explicit, versioned, and easy for future AI agents and humans to consult.

## Start Here

Before making technical recommendations or code changes, read:

- `AGENTS.md`
- `docs/foundation/marco-zero.md`
- `docs/decisions/README.md`

## Decision Model

Architecture and technology decisions are registered as ADRs in:

```txt
docs/decisions/
```

Accepted ADRs are part of the operating rules of the project.

## Prompts And Skills

Versioned prompts live in:

```txt
prompts/
```

Versioned project skills live in:

```txt
skills/<skill-name>/
```

`C:\Users\sergi\.codex\skills\` is only the local Codex runtime location. When
a skill must be available to Codex, sync the versioned source into that runtime
folder:

```powershell
.\tools\sync-codex-skills.ps1
```

Do not manually copy a skill folder into an existing runtime skill folder. That
can create nested duplicates such as `skill-name/skill-name/`.

## Foundation

The formal product foundation is:

```txt
docs/foundation/marco-zero.md
```

It defines the product vision, core principles, MVP boundaries, initial modules,
main entities, core flows, living documentation model, risks, evolution plan,
and rules for future AI agents.
