import { env } from "@/env";
import "./styles.css";
import { AnalyticsProvider } from "@repo/analytics/provider";
import {
  DesignSystemProvider,
  generateTenantCssVariables,
  resolveTenantSubdomain,
  type TenantLookupResponse,
} from "@repo/design-system";
import { fonts } from "@repo/design-system/lib/fonts";
import { Toolbar } from "@repo/feature-flags/components/toolbar";
import { headers } from "next/headers";
import type { ReactNode } from "react";

interface RootLayoutProperties {
  readonly children: ReactNode;
}

const webUrl = env.NEXT_PUBLIC_WEB_URL || "http://localhost:3001";

async function getTenantBranding(subdomain: string): Promise<TenantLookupResponse | null> {
  try {
    const apiUrl =
      process.env.INTERNAL_API_URL ||
      process.env.NEXT_PUBLIC_API_URL ||
      "http://localhost:5000";

    const response = await fetch(
      `${apiUrl}/tenants/lookup?subdomain=${encodeURIComponent(subdomain)}`,
      {
        next: {
          tags: [`tenant-${subdomain}`],
          revalidate: 86400,
        },
      }
    );

    if (!response.ok) {
      return null;
    }

    return (await response.json()) as TenantLookupResponse;
  } catch (error) {
    console.error(`[Branding] Failed to fetch branding for subdomain "${subdomain}":`, error);
    return null;
  }
}

const RootLayout = async ({ children }: RootLayoutProperties) => {
  const headersList = await headers();
  const subdomain = resolveTenantSubdomain(headersList);
  const tenant = subdomain ? await getTenantBranding(subdomain) : null;

  const dynamicCss = generateTenantCssVariables(
    tenant?.branding?.primaryColor,
    tenant?.branding?.secondaryColor
  );

  return (
    <html className={fonts} lang="en" suppressHydrationWarning>
      <head>
        <style
          id="tenant-theme"
          dangerouslySetInnerHTML={{ __html: dynamicCss }}
        />
        {tenant?.branding?.faviconUrl && (
          <link rel="icon" href={tenant.branding.faviconUrl} />
        )}
      </head>
      <body>
        <AnalyticsProvider>
          <DesignSystemProvider
            helpUrl={env.NEXT_PUBLIC_DOCS_URL}
            privacyUrl={new URL("/legal/privacy", webUrl).toString()}
            termsUrl={new URL("/legal/terms", webUrl).toString()}
          >
            {children}
          </DesignSystemProvider>
        </AnalyticsProvider>
        <Toolbar />
      </body>
    </html>
  );
};

export default RootLayout;
