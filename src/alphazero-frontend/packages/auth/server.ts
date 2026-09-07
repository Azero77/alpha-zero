import "server-only";

export interface AuthUser {
  id: string;
  name?: string;
}

export interface AuthResult {
  userId: string | null;
  orgId: string | null;
}

export const auth = async (): Promise<AuthResult> => {
  return {
    userId: null,
    orgId: null,
  };
};

export const currentUser = async (): Promise<AuthUser | null> => {
  return null;
};
