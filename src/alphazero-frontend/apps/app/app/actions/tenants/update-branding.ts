"use server";

import { revalidateTag } from "next/cache";
import { getAccessToken, getTenantId } from "@/lib/session";

export interface UpdateBrandingInput {
  tenantId?: string;
  subdomain?: string;
  name: string;
  primaryColor?: string | null;
  secondaryColor?: string | null;
  logoUrl?: string | null;
  darkModeLogoUrl?: string | null;
  faviconUrl?: string | null;
}

export async function updateTenantBrandingAction(input: UpdateBrandingInput) {
  try {
    const sessionTenantId = await getTenantId();
    const targetTenantId = input.tenantId || sessionTenantId;

    if (!targetTenantId) {
      return { error: "No active tenant found in session." };
    }

    const token = await getAccessToken();
    const apiUrl =
      process.env.INTERNAL_API_URL ||
      process.env.NEXT_PUBLIC_API_URL ||
      "http://localhost:5000";

    const response = await fetch(`${apiUrl}/tenants/${targetTenantId}`, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify({
        id: targetTenantId,
        name: input.name,
        primaryColor: input.primaryColor,
        secondaryColor: input.secondaryColor,
        logoUrl: input.logoUrl,
        darkModeLogoUrl: input.darkModeLogoUrl,
        faviconUrl: input.faviconUrl,
      }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      return {
        error:
          errorData?.title ||
          errorData?.detail ||
          `Failed to update tenant branding (HTTP ${response.status})`,
      };
    }

    // Direct Next.js cache invalidation right after successful API response
    if (input.subdomain) {
      try {
        const normalizedSubdomain = input.subdomain.toLowerCase().trim();
        (revalidateTag as (tag: string, profile?: any) => void)(`tenant-${normalizedSubdomain}`, "default");
      } catch (err) {
        console.warn("Direct Next.js revalidation notice:", err);
      }
    }

    return { success: true };
  } catch (error) {
    return {
      error: error instanceof Error ? error.message : "Failed to update branding.",
    };
  }
}
