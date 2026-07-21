import { describe, it, expect } from "vitest";
import { canAssignProfile } from "./user-profiles";

const base = { canAssign: true, targetIsOwner: false, meIsOwner: false, profileIsSystem: false };

describe("canAssignProfile", () => {
  it("nega sem profiles.assign", () => {
    expect(canAssignProfile({ ...base, canAssign: false })).toBe(false);
  });
  it("nega quando o alvo é o owner (somente leitura)", () => {
    expect(canAssignProfile({ ...base, targetIsOwner: true })).toBe(false);
  });
  it("perfil is_system: nega se o operador não é owner (postura B)", () => {
    expect(canAssignProfile({ ...base, profileIsSystem: true, meIsOwner: false })).toBe(false);
  });
  it("perfil is_system: permite se o operador é owner", () => {
    expect(canAssignProfile({ ...base, profileIsSystem: true, meIsOwner: true })).toBe(true);
  });
  it("perfil comum com profiles.assign: permite", () => {
    expect(canAssignProfile(base)).toBe(true);
  });
});
