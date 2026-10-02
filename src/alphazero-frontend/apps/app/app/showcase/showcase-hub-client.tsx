"use client";

import * as React from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { ComponentsShowcaseClient } from "../(authenticated)/components-showcase/components-showcase-client";
import { VideoPlayerShowcaseClient } from "./video-player-showcase";
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from "@repo/design-system/components/ui/tabs";
import {
  LayersIcon,
  PlayCircleIcon,
  SparklesIcon,
} from "lucide-react";

export function ShowcaseHubClient() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const activeTabParam = searchParams.get("tab") || "lms-components";
  const [activeTab, setActiveTab] = React.useState<string>(
    activeTabParam === "video" || activeTabParam === "video-player"
      ? "video-player"
      : "lms-components"
  );

  const handleTabChange = (val: string) => {
    setActiveTab(val);
    const newTabParam = val === "video-player" ? "video" : "lms";
    router.replace(`/showcase?tab=${newTabParam}`, { scroll: false });
  };

  return (
    <div className="space-y-6">
      {/* Top Global Showcase Navigation Bar */}
      <div className="max-w-7xl mx-auto px-4 md:px-8">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-2 bg-muted/30 border border-border/80 rounded-2xl">
          <Tabs
            value={activeTab}
            onValueChange={handleTabChange}
            className="w-full sm:w-auto"
          >
            <TabsList className="grid grid-cols-2 w-full sm:w-[420px] bg-background/80 border border-border/60">
              <TabsTrigger
                value="lms-components"
                className="text-xs font-semibold gap-1.5 data-[state=active]:bg-primary data-[state=active]:text-primary-foreground"
              >
                <LayersIcon className="size-3.5" />
                <span>1. Core LMS Components</span>
              </TabsTrigger>
              <TabsTrigger
                value="video-player"
                className="text-xs font-semibold gap-1.5 data-[state=active]:bg-primary data-[state=active]:text-primary-foreground"
              >
                <PlayCircleIcon className="size-3.5" />
                <span>2. Secure Video Player</span>
              </TabsTrigger>
            </TabsList>
          </Tabs>

          <div className="hidden sm:flex items-center gap-2 text-xs text-muted-foreground px-3 font-mono">
            <SparklesIcon className="size-3.5 text-primary" />
            <span>AlphaZero Design System Specimens</span>
          </div>
        </div>
      </div>

      {/* Selected Showcase Body */}
      {activeTab === "video-player" ? (
        <VideoPlayerShowcaseClient />
      ) : (
        <ComponentsShowcaseClient />
      )}
    </div>
  );
}
