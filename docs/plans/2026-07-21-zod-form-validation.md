# Validação de Formulários com Zod (conformidade com a ADR-0002)

> **Para agentes:** SUB-SKILL OBRIGATÓRIA: use `superpowers:subagent-driven-development`
> (recomendado) ou `superpowers:executing-plans` para executar tarefa a tarefa.
> Os passos usam checkbox (`- [ ]`) para acompanhamento.

**Objetivo:** colocar `apps/web` em conformidade com a ADR-0002, que determina
que Zod **deve** ser usado para validação de formulários — hoje nenhum
formulário o usa e o pacote sequer está declarado.

**Arquitetura:** os schemas ficam em um módulo puro
(`src/lib/form-schemas.ts`), separado dos componentes, para serem testáveis por
unit sem renderizar React. Cada formulário passa a validar no submit com
`safeParse` e a enviar `parsed.data` — que já chega com `trim` aplicado, o que
elimina os `.trim()` espalhados hoje pelos componentes.

**Stack:** TypeScript, React 19, Next.js 16, Zod 4, Vitest.

**Por que agora:** esta é uma dívida anterior à feature de provisionamento — os
formulários de login, empresa e perfil já nasceram sem Zod. Ela é paga aqui, em
commits próprios, **antes** do frontend da feature, para que o
`UserForm` novo já nasça em conformidade e o diff que a skill
`security-engineer` revisa continue sendo o da feature, não uma mistura.

**Relacionado:** ADR-0002 (base frontend),
`docs/plans/2026-07-21-user-provisioning-frontend.md` (Task 6 depende desta).

## Escopo

Formulários com entrada de texto: **login**, **CompanyForm**, **ProfileForm**.

Fora: `UserProfilesForm`, `UserCompaniesForm` e `CompanyUsersForm` são
checklists de ids — não há entrada livre para validar, e forçar schema neles
seria cerimônia sem ganho.

## Ambiente

```
npm --prefix apps/web run test
npm --prefix apps/web run lint
npm --prefix apps/web run build
```

---

## Task A: instalar Zod e criar os schemas

**Arquivos:**
- Modificar: `apps/web/package.json` (via `npm install`)
- Criar: `apps/web/src/lib/form-schemas.ts`
- Criar: `apps/web/src/lib/form-schemas.test.ts`

- [ ] **Passo 1: instalar a dependência**

```
npm --prefix apps/web install zod@^4
```

Confirme que `zod` apareceu em `dependencies` do
`apps/web/package.json`. Isto é o ponto principal do passo: o pacote **já
existia** em `node_modules` como dependência transitiva do CLI `shadcn`, então
um `import` funcionaria hoje e quebraria sem aviso num `npm prune` ou numa bump
do shadcn. A partir daqui ele é uma dependência declarada.

- [ ] **Passo 2: escrever os testes que falham**

Crie `apps/web/src/lib/form-schemas.test.ts`:

```ts
import { describe, it, expect } from "vitest";
import { loginSchema, companySchema, profileSchema, firstError } from "./form-schemas";

describe("loginSchema", () => {
  it("aceita email e senha preenchidos", () => {
    const r = loginSchema.safeParse({ email: "a@b.local", password: "Passw0rd!" });
    expect(r.success).toBe(true);
  });
  it("rejeita email malformado", () => {
    const r = loginSchema.safeParse({ email: "sem-arroba", password: "Passw0rd!" });
    expect(r.success).toBe(false);
  });
  it("rejeita senha vazia", () => {
    const r = loginSchema.safeParse({ email: "a@b.local", password: "" });
    expect(r.success).toBe(false);
  });
});

describe("companySchema", () => {
  it("apara espaços do nome", () => {
    const r = companySchema.safeParse({ name: "  Acme  " });
    expect(r.success).toBe(true);
    if (r.success) expect(r.data.name).toBe("Acme");
  });
  it("rejeita nome só de espaços", () => {
    expect(companySchema.safeParse({ name: "   " }).success).toBe(false);
  });
  it("rejeita nome acima de 200 caracteres (limite do backend)", () => {
    expect(companySchema.safeParse({ name: "x".repeat(201) }).success).toBe(false);
  });
});

describe("profileSchema", () => {
  it("aceita descrição vazia", () => {
    const r = profileSchema.safeParse({ name: "Financeiro", description: "" });
    expect(r.success).toBe(true);
  });
  it("rejeita nome vazio", () => {
    expect(profileSchema.safeParse({ name: "", description: "x" }).success).toBe(false);
  });
  it("rejeita descrição acima de 500 caracteres (limite do backend)", () => {
    const r = profileSchema.safeParse({ name: "Financeiro", description: "x".repeat(501) });
    expect(r.success).toBe(false);
  });
});

describe("firstError", () => {
  it("devolve a mensagem do primeiro problema", () => {
    const r = companySchema.safeParse({ name: "" });
    expect(r.success).toBe(false);
    if (!r.success) expect(firstError(r.error)).toBe("Informe o nome da empresa.");
  });
});
```

- [ ] **Passo 3: rodar e confirmar que falha**

```
npm --prefix apps/web run test
```

Esperado: `Failed to load ./form-schemas`.

- [ ] **Passo 4: implementar os schemas**

Crie `apps/web/src/lib/form-schemas.ts`:

```ts
import { z } from "zod";

/**
 * Schemas de formulário (ADR-0002). Vivem fora dos componentes para serem
 * testados sem renderizar React, e para que os limites de tamanho fiquem num
 * lugar só — eles espelham as colunas do backend, que continua sendo a
 * validação de verdade. Este módulo é conveniência de UX, não barreira.
 */

export const loginSchema = z.object({
  email: z.email({ message: "Informe um email válido." }),
  password: z.string().min(1, "Informe a senha."),
});

export const companySchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Informe o nome da empresa.")
    .max(200, "O nome deve ter no máximo 200 caracteres."),
});

export const profileSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Informe o nome do perfil.")
    .max(200, "O nome deve ter no máximo 200 caracteres."),
  description: z
    .string()
    .trim()
    .max(500, "A descrição deve ter no máximo 500 caracteres."),
});

export const userSchema = z
  .object({
    fullName: z
      .string()
      .trim()
      .min(1, "Informe o nome completo.")
      .max(200, "O nome deve ter no máximo 200 caracteres."),
    email: z.email({ message: "Informe um email válido." }),
    // A política real (maiúscula, número, símbolo) é do Identity, no backend:
    // duplicá-la aqui criaria duas fontes de verdade que divergem no dia em que
    // a configuração do Identity mudar. Aqui só o mínimo que evita ida à API.
    password: z.string().min(8, "A senha deve ter ao menos 8 caracteres."),
    confirm: z.string(),
  })
  .refine((v) => v.password === v.confirm, {
    message: "As senhas não conferem.",
    path: ["confirm"],
  });

export type LoginInput = z.output<typeof loginSchema>;
export type CompanyInput = z.output<typeof companySchema>;
export type ProfileInput = z.output<typeof profileSchema>;
export type UserInput = z.output<typeof userSchema>;

/** Primeira mensagem de erro, para exibir no `role="alert"` do formulário. */
export function firstError(error: z.ZodError): string {
  return error.issues[0]?.message ?? "Verifique os dados informados.";
}
```

- [ ] **Passo 5: rodar e confirmar que passa**

```
npm --prefix apps/web run test
```

Esperado: os 10 testes novos verdes, somados aos 27 já existentes.

Se `z.email` não existir, a versão instalada é Zod 3 — confira o `package.json`
e reinstale com `zod@^4`. Em Zod 3 a forma seria `z.string().email()`, mas o
plano assume a 4, que é a que já está na árvore de dependências.

- [ ] **Passo 6: commit**

```bash
git add apps/web/package.json apps/web/package-lock.json apps/web/src/lib/form-schemas.ts apps/web/src/lib/form-schemas.test.ts
git commit -m "feat(web): declara zod e cria os schemas de formulario

A ADR-0002 exige Zod em formularios, mas nenhum usava e o pacote so
existia como transitivo do CLI shadcn — um import funcionaria hoje e
quebraria num npm prune. Passa a ser dependencia declarada, com os
schemas isolados em modulo puro e testado.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task B: retrofit do login

**Arquivos:**
- Modificar: `apps/web/src/app/(auth)/login/page.tsx`

- [ ] **Passo 1: aplicar o schema**

Em `apps/web/src/app/(auth)/login/page.tsx`, adicione o import:

```tsx
import { loginSchema, firstError } from "@/lib/form-schemas";
```

E substitua o corpo de `handleSubmit` por:

```tsx
  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    const parsed = loginSchema.safeParse({ email, password });
    if (!parsed.success) {
      setError(firstError(parsed.error));
      return;
    }

    setLoading(true);
    try {
      await api.post<{ user: User }>("/auth/login", parsed.data);
      router.push("/companies");
      router.refresh();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setLoading(false);
    }
  }
```

Mantenha os atributos `required` e `type="email"` nos inputs: eles continuam
sendo a primeira linha de UX do navegador, e o schema é a rede embaixo.

- [ ] **Passo 2: verificar**

```
npm --prefix apps/web run lint
npm --prefix apps/web run build
```

- [ ] **Passo 3: commit**

```bash
git add apps/web/src/app/(auth)/login/page.tsx
git commit -m "refactor(web): valida o login com zod

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task C: retrofit do CompanyForm

**Arquivos:**
- Modificar: `apps/web/src/components/CompanyForm.tsx`

- [ ] **Passo 1: aplicar o schema**

Em `apps/web/src/components/CompanyForm.tsx`, adicione o import:

```tsx
import { companySchema, firstError } from "@/lib/form-schemas";
```

E substitua `handleSubmit` por:

```tsx
  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    // O schema já apara o nome, então o .trim() manual sai daqui.
    const parsed = companySchema.safeParse({ name });
    if (!parsed.success) {
      setError(firstError(parsed.error));
      return;
    }

    setLoading(true);
    try {
      await onSubmit(parsed.data.name);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setLoading(false);
    }
  }
```

- [ ] **Passo 2: verificar**

```
npm --prefix apps/web run lint
npm --prefix apps/web run build
```

- [ ] **Passo 3: commit**

```bash
git add apps/web/src/components/CompanyForm.tsx
git commit -m "refactor(web): valida o formulario de empresa com zod

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task D: retrofit do ProfileForm

**Arquivos:**
- Modificar: `apps/web/src/components/ProfileForm.tsx`

- [ ] **Passo 1: aplicar o schema**

Em `apps/web/src/components/ProfileForm.tsx`, adicione o import:

```tsx
import { profileSchema, firstError } from "@/lib/form-schemas";
```

E substitua `handleSubmit` por:

```tsx
  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    const parsed = profileSchema.safeParse({ name, description });
    if (!parsed.success) {
      setError(firstError(parsed.error));
      return;
    }

    setSaving(true);
    const body = { ...parsed.data, permissionKeys: [...selected] };
    try {
      if (mode === "create") await api.post("/profiles", body);
      else await api.put(`/profiles/${profile.id}`, body);
      router.push("/profiles");
      router.refresh();
    } catch (err) {
      setError(errorMessage(err));
      setSaving(false);
    }
  }
```

Repare que o `setSaving(true)` desceu para **depois** da validação: hoje ele
sobe antes, então um nome inválido deixaria o botão em "Salvando..." sem que
nada estivesse salvando.

- [ ] **Passo 2: verificar**

```
npm --prefix apps/web run lint
npm --prefix apps/web run build
```

- [ ] **Passo 3: commit**

```bash
git add apps/web/src/components/ProfileForm.tsx
git commit -m "refactor(web): valida o formulario de perfil com zod

Corrige de passagem o estado Salvando... que ficava preso quando a
validacao falhava antes de qualquer chamada.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task E: verificação e registro da decisão

- [ ] **Passo 1: suíte completa do frontend**

```
npm --prefix apps/web run test
npm --prefix apps/web run lint
npm --prefix apps/web run build
```

- [ ] **Passo 2: e2e (os fluxos de login, empresa e perfil foram tocados)**

```
docker compose up -d db api
npm --prefix apps/web run e2e
```

Esperado: 12 passed, como antes do retrofit. Qualquer falha aqui é regressão
introduzida pela validação — investigue antes de seguir.

- [ ] **Passo 3: registrar a conformidade**

Em `docs/decisions/ADR-0002-frontend-base.md`, na seção **Consequencias**, logo
após a linha sobre Zod, acrescente:

```markdown
  Status: aplicado em `apps/web` em 2026-07-21 (schemas em
  `src/lib/form-schemas.ts`, cobrindo login, empresa, perfil e usuario).
  Checklists de ids ficam de fora por nao terem entrada livre.
```

- [ ] **Passo 4: commit**

```bash
git add docs/decisions/ADR-0002-frontend-base.md
git commit -m "docs(adr): registra a aplicacao do Zod prevista na ADR-0002

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

## Próximo plano

`docs/plans/2026-07-21-user-provisioning-frontend.md` — a Task 6 já constrói o
`UserForm` usando o `userSchema` criado aqui.
