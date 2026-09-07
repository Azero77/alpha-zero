"use server";

export const searchUsers = async (
  _query: string
): Promise<
  | {
      data: string[];
    }
  | {
      error: unknown;
    }
> => {
  return { data: [] };
};
