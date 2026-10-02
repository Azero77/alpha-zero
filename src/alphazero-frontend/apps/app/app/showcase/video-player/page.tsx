import type { Metadata } from "next";
import Link from "next/link";
import { ArrowLeftIcon } from "lucide-react";
import { VideoPlayerShowcaseClient } from "../video-player-showcase";

export const metadata: Metadata = {
  title: "Secure Video Player Showcase | AlphaZero Academy",
  description: "Interactive laboratory for encrypted HLS streaming, 2D Brownian canvas watermarking, and Cloudflare capability tokens.",
};

export default function VideoPlayerShowcasePage() {
  return (
    <main className="min-h-screen bg-background text-foreground py-6">
      <div className="max-w-7xl mx-auto px-4 md:px-8 mb-4">
        <Link
          href="/showcase"
          className="inline-flex items-center gap-1.5 text-xs font-medium text-muted-foreground hover:text-foreground transition-colors"
        >
          <ArrowLeftIcon className="size-3.5" />
          <span>Back to LMS Components Showcase</span>
        </Link>
      </div>
      <VideoPlayerShowcaseClient />
    </main>
  );
}
