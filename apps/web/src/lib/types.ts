export type User = {
  id: string;
  email: string;
  fullName: string;
};

export type Company = {
  id: string;
  name: string;
  status: string;
  createdAt: string;
  updatedAt: string;
};

export type Me = {
  user: User;
  organizationId: string;
  isOwner: boolean;
  permissions: string[];
  companies: Company[];
};

export type PermissionItem = { key: string; displayName: string; description: string };
export type PermissionGroup = { module: string; permissions: PermissionItem[] };

export type ProfileSummary = {
  id: string;
  name: string;
  description: string;
  isSystem: boolean;
  status: string;
  userCount: number;
};

export type ProfileDetail = {
  id: string;
  name: string;
  description: string;
  isSystem: boolean;
  status: string;
  permissionKeys: string[];
};

export type EntityRef = { id: string; name: string };

export type UserRow = {
  id: string;
  fullName: string;
  email: string;
  status: string;
  isOwner: boolean;
  profiles: EntityRef[];
  companies: EntityRef[];
};

export type CompanyUserAccess = {
  id: string;
  fullName: string;
  email: string;
  hasAccess: boolean;
};
