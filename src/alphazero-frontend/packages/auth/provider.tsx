"use client";

import type { ReactNode } from "react";

export interface AuthProviderProperties {
  readonly children: ReactNode;
  readonly privacyUrl?: string;
  readonly termsUrl?: string;
  readonly helpUrl?: string;
}

export const AuthProvider = ({ children }: AuthProviderProperties) => {
  return <>{children}</>;
};
