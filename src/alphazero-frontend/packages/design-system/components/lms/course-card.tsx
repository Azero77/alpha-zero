"use client";

import * as React from "react";
import Image from "next/image";
import { BookOpenIcon, ClockIcon, PlayCircleIcon, UserIcon } from "lucide-react";
import { cn } from "@repo/design-system/lib/utils";
import { BitmaskProgressBar } from "./bitmask-progress-bar";

export interface CourseCardProps {
  /**
   * Primary course title.
   */
  title: string;
  /**
   * Thumbnail image URL. Displays a fallback preview if not provided.
   */
  thumbnailUrl?: string;
  /**
   * Subject track or category name (e.g. "Mathematics", "Computer Science").
   */
  category?: string;
  /**
   * Course instructor details.
   */
  instructor?: {
    name: string;
    avatarUrl?: string;
    role?: string;
  };
  /**
   * Total number of lessons in the syllabus.
   */
  lessonCount?: number;
  /**
   * Total course runtime/duration (e.g. "4.5 hrs").
   */
  duration?: string;
  /**
   * Progress bitmask payload for enrolled students.
   */
  progress?: {
    bitmask: string;
    totalLessons?: number;
  };
  /**
   * Whether the current student is enrolled in this course.
   * @default false
   */
  isEnrolled?: boolean;
  /**
   * Pricing or access tier label (e.g. "Free", "Library Code", or "$25").
   */
  price?: string;
  /**
   * Custom CTA button label.
   */
  ctaLabel?: string;
  /**
   * CTA button click handler.
   */
  onCtaClick?: () => void;
  /**
   * Layout presentation style:
   * - "vertical": Default 16:9 vertical card for course catalogs and grid listings.
   * - "horizontal": Compact horizontal row optimized for student dashboard lists.
   * @default "vertical"
   */
  layout?: "vertical" | "horizontal";
  /**
   * Additional container CSS classes.
   */
  className?: string;
}

export function CourseCard({
  title,
  thumbnailUrl,
  category,
  instructor,
  lessonCount,
  duration,
  progress,
  isEnrolled = false,
  price,
  ctaLabel,
  onCtaClick,
  layout = "vertical",
  className,
}: CourseCardProps) {
  const defaultCtaText = isEnrolled ? "Continue Lesson" : "Enroll Now";
  const buttonText = ctaLabel ?? defaultCtaText;

  // Thumbnail Container Component
  const Thumbnail = (
    <div className="relative aspect-video w-full overflow-hidden rounded-lg bg-muted/40 border border-border/40 select-none">
      {thumbnailUrl ? (
        <Image
          src={thumbnailUrl}
          alt={title}
          fill
          sizes="(max-width: 768px) 100vw, (max-width: 1200px) 50vw, 33vw"
          className="object-cover transition-transform duration-300 group-hover:scale-105"
        />
      ) : (
        <div className="flex h-full w-full items-center justify-center bg-gradient-to-br from-card to-muted/60 text-muted-foreground/40">
          <PlayCircleIcon className="size-10 stroke-[1.25]" />
        </div>
      )}

      {category && (
        <div className="absolute start-2.5 top-2.5 z-10">
          <span className="rounded-full bg-background/80 backdrop-blur-sm px-2.5 py-0.5 text-[11px] font-medium text-foreground border border-border/60 shadow-xs">
            {category}
          </span>
        </div>
      )}

      {duration && (
        <div className="absolute bottom-2 end-2 z-10">
          <span className="flex items-center gap-1 rounded bg-background/85 backdrop-blur-sm px-1.5 py-0.5 font-mono text-[10px] text-muted-foreground border border-border/40">
            <ClockIcon className="size-2.5" />
            <span>{duration}</span>
          </span>
        </div>
      )}
    </div>
  );

  // Horizontal Layout (Compact Student Dashboard List)
  if (layout === "horizontal") {
    return (
      <div
        data-testid="course-card-horizontal"
        className={cn(
          "group flex flex-col sm:flex-row items-stretch gap-4 p-3.5 rounded-xl border border-border/80 bg-card hover:border-border transition-all duration-200 hover:shadow-xs",
          className
        )}
      >
        <div className="w-full sm:w-48 md:w-56 shrink-0">
          {Thumbnail}
        </div>

        <div className="flex flex-1 flex-col justify-between gap-2.5 min-w-0 text-start">
          <div className="space-y-1">
            <h3 className="font-medium text-sm md:text-base text-foreground line-clamp-2 tracking-tight group-hover:text-primary transition-colors">
              {title}
            </h3>

            {instructor && (
              <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
                <UserIcon className="size-3.5 opacity-60" />
                <span>{instructor.name}</span>
                {lessonCount !== undefined && (
                  <>
                    <span className="opacity-40">•</span>
                    <span>{lessonCount} lessons</span>
                  </>
                )}
              </div>
            )}
          </div>

          {progress && (
            <div className="w-full max-w-md">
              <BitmaskProgressBar
                bitmask={progress.bitmask}
                totalLessons={progress.totalLessons ?? lessonCount}
                size="sm"
                label="Progress"
              />
            </div>
          )}

          <div className="flex items-center justify-between gap-3 pt-1 border-t border-border/40 mt-auto">
            {price && !isEnrolled && (
              <span className="font-mono text-xs font-semibold text-foreground">
                {price}
              </span>
            )}
            <button
              type="button"
              onClick={onCtaClick}
              className="ms-auto inline-flex items-center justify-center rounded-md bg-primary px-3 py-1.5 text-xs font-medium text-primary-foreground hover:bg-primary/90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary transition-colors cursor-pointer"
            >
              {buttonText}
            </button>
          </div>
        </div>
      </div>
    );
  }

  // Vertical Layout (Standard Grid Card)
  return (
    <div
      data-testid="course-card-vertical"
      className={cn(
        "group flex flex-col rounded-xl border border-border/80 bg-card p-3.5 hover:border-border transition-all duration-200 hover:shadow-xs text-start",
        className
      )}
    >
      {Thumbnail}

      <div className="flex flex-1 flex-col justify-between gap-3 pt-3">
        <div className="space-y-1.5">
          <h3 className="font-medium text-sm md:text-base text-foreground line-clamp-2 tracking-tight group-hover:text-primary transition-colors">
            {title}
          </h3>

          <div className="flex items-center gap-2 text-xs text-muted-foreground flex-wrap">
            {instructor && (
              <span className="truncate max-w-[140px]">{instructor.name}</span>
            )}
            {lessonCount !== undefined && (
              <>
                <span className="opacity-40">•</span>
                <span className="flex items-center gap-1 font-mono text-[11px]">
                  <BookOpenIcon className="size-3 opacity-60" />
                  <span>{lessonCount} lessons</span>
                </span>
              </>
            )}
          </div>
        </div>

        {progress && (
          <div className="pt-1">
            <BitmaskProgressBar
              bitmask={progress.bitmask}
              totalLessons={progress.totalLessons ?? lessonCount}
              size="sm"
              label="Progress"
            />
          </div>
        )}

        <div className="flex items-center justify-between gap-2 pt-2.5 border-t border-border/40 mt-auto">
          {price && !isEnrolled ? (
            <span className="font-mono text-xs font-semibold text-foreground">
              {price}
            </span>
          ) : (
            <span />
          )}

          <button
            type="button"
            onClick={onCtaClick}
            className="inline-flex w-full sm:w-auto items-center justify-center rounded-md bg-primary px-3.5 py-1.5 text-xs font-medium text-primary-foreground hover:bg-primary/90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary transition-colors cursor-pointer"
          >
            {buttonText}
          </button>
        </div>
      </div>
    </div>
  );
}
