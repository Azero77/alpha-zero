import { createEnv } from "@t3-oss/env-nextjs";
import { z } from "zod";

export const keys = () =>
  createEnv({
    skipValidation: process.env.SKIP_ENV_VALIDATION === "true",
    server: {
      API_BASE_URL: z.string().url(),
    },

    client: {},

    runtimeEnv: {
      API_BASE_URL: process.env.API_BASE_URL,
    },
  });
