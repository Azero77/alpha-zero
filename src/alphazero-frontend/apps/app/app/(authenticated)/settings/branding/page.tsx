import type { Metadata } from "next";
import { headers } from "next/headers";
import { getTenantId } from "@/lib/session";
import { Header } from "../../components/header";
import { BrandingClient } from "./branding-client";
import { resolveTenantSubdomain, type TenantLookupResponse } from "@repo/design-system/lib/tenant-utils";

export const metadata: Metadata = {
  title: "White-Label & Dynamic Branding | AlphaZero LMS",
  description: "Configure multi-tenant dynamic brand colors, logos, and real-time WCAG contrast safety.",
};

async function getTenantData(tenantId: string | null, subdomain: string | null) {
  const apiUrl =
    process.env.INTERNAL_API_URL ||
    process.env.NEXT_PUBLIC_API_URL ||
    "http://localhost:5000";

  // 1. Try lookup by subdomain first
  if (subdomain) {
    try {
      const res = await fetch(`${apiUrl}/tenants/lookup?subdomain=${encodeURIComponent(subdomain)}`, {
        next: { tags: [`tenant-${subdomain}`], revalidate: 60 },
      });
      if (res.ok) {
        const data = (await res.json()) as TenantLookupResponse;
        return {
          id: data.id,
          name: data.name,
          subdomain: data.subdomain,
          primaryColor: data.branding?.primaryColor || "#2563eb",
          secondaryColor: data.branding?.secondaryColor || null,
          logoUrl: data.branding?.logoUrl || null,
          darkModeLogoUrl: data.branding?.darkModeLogoUrl || null,
          faviconUrl: data.branding?.faviconUrl || null,
        };
      }
    } catch {
      // Fallback
    }
  }

  // 2. Default fallback representation
  return {
    id: tenantId || "00000000-0000-0000-0000-000000000001",
    name: "AlphaZero Academy",
    subdomain: subdomain || "demo",
    primaryColor: "#2563eb",
    secondaryColor: null,
    logoUrl: null,
    darkModeLogoUrl: null,
    faviconUrl: null,
  };
}

export default async function BrandingPage() {
  const headersList = await headers();
  const subdomain = resolveTenantSubdomain(headersList);
  const tenantId = await getTenantId();

  const tenant = await getTenantData(tenantId, subdomain);

  return (
    <>
      <Header page="Dynamic Branding" pages={["Settings", "Academy"]} />
      <div className="flex flex-1 flex-col gap-6 p-6 pt-2 max-w-7xl w-full mx-auto">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">White-Label Academy Branding</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Configure your custom primary and secondary brand colors. Dynamic CSS injection ensures zero FOUC
            and automatic WCAG 2.1 contrast compliance without compiling runtime stylesheets.
          </p>
        </div>

        <BrandingClient initialTenant={tenant} />
      </div>
    </>
  );
}
