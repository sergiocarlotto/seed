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
