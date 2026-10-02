import { createEnv } from "@t3-oss/env-nextjs";

export const keys = () =>
  createEnv({
    skipValidation: process.env.SKIP_ENV_VALIDATION === "true",
    server: {},
    client: {},
    runtimeEnv: {},
  });
