"use client";

import type { ReactNode } from "react";

export interface UserButtonProps {
  readonly showName?: boolean;
  readonly appearance?: Record<string, unknown>;
}

export const UserButton = ({ showName }: UserButtonProps) => {
  return (
    <div className="flex items-center gap-2 px-2 py-1.5 text-sm font-medium">
      <div className="flex h-7 w-7 items-center justify-center rounded-full bg-primary/10 text-primary text-xs font-semibold">
        AZ
      </div>
      {showName && <span className="truncate text-foreground">User</span>}
    </div>
  );
};

export interface OrganizationSwitcherProps {
  readonly hidePersonal?: boolean;
  readonly afterSelectOrganizationUrl?: string;
}

export const OrganizationSwitcher = (_props: OrganizationSwitcherProps) => {
  return (
    <div className="flex items-center gap-2 px-2 py-1 text-sm font-semibold text-foreground">
      <span>AlphaZero Academy</span>
    </div>
  );
};
