import type { Metadata } from "next";
import { Suspense } from "react";
import { ShowcaseHubClient } from "./showcase-hub-client";

export const metadata: Metadata = {
  title: "LMS Components & Video Player Showcase | AlphaZero Academy",
  description: "Live interactive inspection sandbox for AlphaZero LMS domain components and secure video player.",
};

export default function ShowcasePage() {
  return (
    <main className="min-h-screen bg-background text-foreground py-6">
      <Suspense fallback={<div className="p-8 text-center text-sm text-muted-foreground">Loading Showcase Laboratory...</div>}>
        <ShowcaseHubClient />
      </Suspense>
    </main>
  );
}
