export type User = {
  id: string;
  email: string;
  fullName: string;
};

export type Membership = {
  organizationId: string;
  organizationName: string;
  role: string;
};

export type Organization = {
  id: string;
  name: string;
  status: string;
  role: string;
  createdAt: string;
  updatedAt: string;
};
