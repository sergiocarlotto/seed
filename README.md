# seed-skill

Seed project for a durable, AI-assisted web product.

The project is being defined from a Marco Zero foundation: decisions should be
explicit, versioned, and easy for future AI agents and humans to consult.

## Start Here

Before making technical recommendations or code changes, read:

- `CLAUDE.md`
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
.claude/skills/<skill-name>/
```

Claude Code discovers these project skills automatically when you work inside
this repository. There is no runtime sync step: the versioned folder under
`.claude/skills/` is the single source of truth and the location Claude loads
from directly.

## Foundation

The formal product foundation is:

```txt
docs/foundation/marco-zero.md
```

It defines the product vision, core principles, MVP boundaries, initial modules,
main entities, core flows, living documentation model, risks, evolution plan,
and rules for future AI agents.
