import type { Metadata } from "next";
import { ComponentsShowcaseClient } from "../(authenticated)/components-showcase/components-showcase-client";

export const metadata: Metadata = {
  title: "LMS Components Showcase | AlphaZero Academy",
  description: "Live interactive inspection sandbox for AlphaZero LMS domain components.",
};

export default function ShowcasePage() {
  return (
    <main className="min-h-screen bg-background text-foreground py-8">
      <ComponentsShowcaseClient />
    </main>
  );
}
