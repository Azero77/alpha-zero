"use client";

import { useEffect, useRef } from "react";

export interface WatermarkData {
  userId: string;
  userName: string;
  userPhone?: string | null;
  tenantId?: string;
  sessionId?: string;
}

export interface DynamicVisibleWatermarkProps {
  data: WatermarkData;
  opacity?: number;
  className?: string;
}

/**
 * Floating dynamic canvas watermark with 2D Brownian motion drift.
 * Renders subtle, semi-transparent identity markers that drift slowly
 * to deter screen recording and attribution leaks while resisting static video masks.
 */
export function DynamicVisibleWatermark({ data, opacity = 0.22, className }: DynamicVisibleWatermarkProps) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    let animationFrameId: number;
    let width = (canvas.width = canvas.parentElement?.clientWidth || 640);
    let height = (canvas.height = canvas.parentElement?.clientHeight || 360);

    const handleResize = () => {
      if (!canvas || !canvas.parentElement) return;
      width = canvas.width = canvas.parentElement.clientWidth;
      height = canvas.height = canvas.parentElement.clientHeight;
    };

    window.addEventListener("resize", handleResize);

    // Initial position & velocity for 2D Brownian motion drift
    let posX = Math.random() * (width - 200) + 20;
    let posY = Math.random() * (height - 80) + 40;
    let velX = (Math.random() - 0.5) * 0.4;
    let velY = (Math.random() - 0.5) * 0.4;

    const shortId = data.userId.slice(0, 8);
    const shortSession = data.sessionId ? data.sessionId.slice(0, 8) : "";
    const phoneDisplay = data.userPhone || "";

    const render = () => {
      ctx.clearRect(0, 0, width, height);

      // Random Brownian drift perturbations
      velX += (Math.random() - 0.5) * 0.05;
      velY += (Math.random() - 0.5) * 0.05;

      // Velocity dampening
      const maxSpeed = 0.5;
      velX = Math.max(-maxSpeed, Math.min(maxSpeed, velX));
      velY = Math.max(-maxSpeed, Math.min(maxSpeed, velY));

      posX += velX;
      posY += velY;

      // Soft boundary bouncing
      const margin = 20;
      if (posX < margin) {
        posX = margin;
        velX = Math.abs(velX);
      } else if (posX > width - 180) {
        posX = width - 180;
        velX = -Math.abs(velX);
      }

      if (posY < margin + 20) {
        posY = margin + 20;
        velY = Math.abs(velY);
      } else if (posY > height - 40) {
        posY = height - 40;
        velY = -Math.abs(velY);
      }

      // Draw subtle watermark text
      ctx.save();
      ctx.font = "11px Inter, system-ui, sans-serif";
      const baseOpacity = Math.max(0.02, Math.min(1.0, opacity));
      ctx.fillStyle = `rgba(255, 255, 255, ${baseOpacity})`;
      ctx.shadowColor = "rgba(0, 0, 0, 0.4)";
      ctx.shadowBlur = 2;
      ctx.shadowOffsetX = 1;
      ctx.shadowOffsetY = 1;

      const dateStr = new Date().toLocaleTimeString();
      const line1 = `${data.userName} • ${shortId}`;
      const line2 = phoneDisplay ? `${phoneDisplay} • ${dateStr}` : `${shortSession} • ${dateStr}`;

      ctx.fillText(line1, posX, posY);
      ctx.fillText(line2, posX, posY + 14);

      // Render secondary ghost watermark at opposite quadrant to prevent cropping
      const ghostX = (posX + width / 2) % (width - 150) + 20;
      const ghostY = (posY + height / 2) % (height - 60) + 30;
      ctx.fillStyle = `rgba(255, 255, 255, ${baseOpacity * 0.65})`;
      ctx.fillText(line1, ghostX, ghostY);
      ctx.fillText(line2, ghostX, ghostY + 14);

      ctx.restore();

      animationFrameId = requestAnimationFrame(render);
    };

    render();

    return () => {
      cancelAnimationFrame(animationFrameId);
      window.removeEventListener("resize", handleResize);
    };
  }, [data, opacity]);

  return (
    <canvas
      ref={canvasRef}
      className={`pointer-events-none absolute inset-0 z-30 h-full w-full select-none ${className || ""}`}
    />
  );
}
