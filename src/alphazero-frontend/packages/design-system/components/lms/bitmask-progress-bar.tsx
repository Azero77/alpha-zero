"use client";

import * as React from "react";
import { CheckIcon, LockIcon } from "lucide-react";
import { cn } from "@repo/design-system/lib/utils";

export interface BitmaskProgressBarProps extends React.HTMLAttributes<HTMLDivElement> {
  /**
   * The binary completion bitmask string from the database (e.g. "11110010").
   * '1' represents a completed lesson, '0' represents an uncompleted/locked lesson.
   */
  bitmask: string;
  /**
   * Total number of lessons. Defaults to bitmask.length if omitted.
   */
  totalLessons?: number;
  /**
   * Display variant:
   * - "continuous": Sleek progress track with Precision Blue fill (Success Green at 100%) and monospace counter.
   * - "segmented": Array of discrete pill capsules for each lesson bit.
   * @default "continuous"
   */
  variant?: "continuous" | "segmented";
  /**
   * Whether to display the lesson counter and percentage.
   * @default true
   */
  showCounter?: boolean;
  /**
   * Accessible label for screen readers.
   * @default "Course progress"
   */
  label?: string;
  /**
   * Visual density sizing.
   * @default "md"
   */
  size?: "sm" | "md" | "lg";
}

export function BitmaskProgressBar({
  bitmask = "",
  totalLessons,
  variant = "continuous",
  showCounter = true,
  label = "Course progress",
  size = "md",
  className,
  ...props
}: BitmaskProgressBarProps) {
  const safeBitmask = typeof bitmask === "string" ? bitmask : "";
  const total = Math.max(totalLessons ?? safeBitmask.length, 1);

  // Calculate completed count up to the declared total
  const completedCount = React.useMemo(() => {
    let count = 0;
    for (let i = 0; i < Math.min(safeBitmask.length, total); i++) {
      if (safeBitmask.charAt(i) === "1") {
        count++;
      }
    }
    return count;
  }, [safeBitmask, total]);

  const percentage = Math.min(100, Math.round((completedCount / total) * 100));
  const isAllComplete = completedCount === total && total > 0;

  // Track the first locked lesson index (the current active lesson)
  const activeLessonIndex = React.useMemo(() => {
    for (let i = 0; i < total; i++) {
      if (i >= safeBitmask.length || safeBitmask.charAt(i) !== "1") {
        return i;
      }
    }
    return -1; // All completed
  }, [safeBitmask, total]);

  const trackHeightClass = {
    sm: "h-1",
    md: "h-1.5",
    lg: "h-2.5",
  }[size];

  if (variant === "segmented") {
    return (
      <div
        className={cn("w-full space-y-2", className)}
        role="progressbar"
        aria-valuenow={percentage}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={label}
        {...props}
      >
        {showCounter && (
          <div className="flex items-center justify-between text-xs text-muted-foreground">
            <span className="font-medium text-foreground">{label}</span>
            <span className="font-mono tracking-tight" data-testid="bitmask-counter">
              {completedCount}/{total} ({percentage}%)
            </span>
          </div>
        )}

        <div
          className="flex items-center gap-1.5 flex-wrap"
          data-testid="segmented-rail"
        >
          {Array.from({ length: total }, (_, i) => {
            const isCompleted = i < safeBitmask.length && safeBitmask.charAt(i) === "1";
            const isActive = i === activeLessonIndex;

            return (
              <div
                key={i}
                data-testid={`bitmask-segment-${i}`}
                data-status={isCompleted ? "completed" : isActive ? "active" : "locked"}
                title={`Lesson ${i + 1}: ${isCompleted ? "Completed" : isActive ? "Current" : "Locked"}`}
                className={cn(
                  "relative flex items-center justify-center rounded-sm transition-all text-xs font-mono",
                  size === "sm" && "h-5 min-w-5 px-1 text-[10px]",
                  size === "md" && "h-6 min-w-6 px-1.5 text-xs",
                  size === "lg" && "h-7 min-w-7 px-2 text-xs",
                  isCompleted && "bg-success/15 text-success border border-success/30 font-semibold",
                  isActive && "bg-primary/15 text-primary border border-primary/40 ring-1 ring-primary/30 font-semibold",
                  !isCompleted && !isActive && "bg-muted/40 text-muted-foreground/50 border border-border"
                )}
              >
                {isCompleted ? (
                  <CheckIcon className={cn("size-3", size === "lg" && "size-3.5")} />
                ) : isActive ? (
                  <span className="size-1.5 rounded-full bg-primary animate-pulse" />
                ) : (
                  <LockIcon className={cn("size-2.5 opacity-50", size === "lg" && "size-3")} />
                )}
              </div>
            );
          })}
        </div>
      </div>
    );
  }

  // Continuous Variant (Default)
  return (
    <div
      className={cn("w-full space-y-1.5", className)}
      role="progressbar"
      aria-valuenow={percentage}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-label={label}
      {...props}
    >
      {showCounter && (
        <div className="flex items-center justify-between text-xs text-muted-foreground">
          <span className="text-foreground/90 text-start">{label}</span>
          <span className="font-mono text-muted-foreground" data-testid="bitmask-counter">
            {completedCount}/{total} ({percentage}%)
          </span>
        </div>
      )}

      <div
        className={cn(
          "w-full overflow-hidden rounded-full bg-secondary/80 border border-border/40",
          trackHeightClass
        )}
      >
        <div
          data-testid="bitmask-continuous-fill"
          data-all-complete={isAllComplete}
          className={cn(
            "h-full rounded-full transition-all duration-500 ease-out",
            isAllComplete
              ? "bg-success"
              : "bg-primary"
          )}
          style={{ width: `${percentage}%` }}
        />
      </div>
    </div>
  );
}
