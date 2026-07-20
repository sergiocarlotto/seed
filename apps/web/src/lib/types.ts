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
