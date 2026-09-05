"use client";

import type { ReactNode } from "react";

interface NotificationsProviderProperties {
  children: ReactNode;
  userId: string;
}

export const NotificationsProvider = ({
  children,
}: NotificationsProviderProperties) => {
  return <>{children}</>;
};
