import { keys as analytics } from "@repo/analytics/keys";
import { keys as apiClient } from "@repo/api-client/keys";
import { keys as auth } from "@repo/auth/keys";
import { keys as flags } from "@repo/feature-flags/keys";
import { keys as core } from "@repo/next-config/keys";
import { keys as observability } from "@repo/observability/keys";
import { keys as security } from "@repo/security/keys";
import { keys as webhooks } from "@repo/webhooks/keys";
import { createEnv } from "@t3-oss/env-nextjs";

export const env = createEnv({
  emptyStringAsUndefined: true,
  skipValidation: process.env.SKIP_ENV_VALIDATION === "true",
  extends: [
    apiClient(),
    auth(),
    analytics(),
    core(),
    flags(),
    observability(),
    security(),
    webhooks(),
  ],
  server: {},
  client: {},
  runtimeEnv: {},
});
