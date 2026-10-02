"use server";

export interface UserInfo {
  color: string;
  name: string;
  picture: string;
}

export const getUsers = async (
  _userIds: string[]
): Promise<
  | {
      data: UserInfo[];
    }
  | {
      error: unknown;
    }
> => {
  return { data: [] };
};
