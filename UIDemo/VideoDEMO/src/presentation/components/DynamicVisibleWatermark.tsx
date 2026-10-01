import React, { useEffect, useRef } from 'react';

interface WatermarkData {
  fullName: string;
  phone: string;
  userId: string;
  sessionId: string;
}

interface DynamicVisibleWatermarkProps {
  data: WatermarkData;
  className?: string;
}

export const DynamicVisibleWatermark: React.FC<DynamicVisibleWatermarkProps> = ({ data, className = '' }) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    let animationFrameId: number;
    let x = Math.random() * (canvas.width - 200);
    let y = Math.random() * (canvas.height - 100);
    
    // Brownian motion variables
    let vx = (Math.random() - 0.5) * 1;
    let vy = (Math.random() - 0.5) * 1;

    const render = () => {
      // Resize canvas to match display size
      const { width, height } = canvas.getBoundingClientRect();
      if (canvas.width !== width || canvas.height !== height) {
        canvas.width = width;
        canvas.height = height;
      }

      ctx.clearRect(0, 0, canvas.width, canvas.height);

      // Update position (Brownian motion drift)
      x += vx;
      y += vy;

      // Add random perturbations
      vx += (Math.random() - 0.5) * 0.1;
      vy += (Math.random() - 0.5) * 0.1;

      // Limit speed
      const speedLimit = 0.5;
      vx = Math.max(-speedLimit, Math.min(speedLimit, vx));
      vy = Math.max(-speedLimit, Math.min(speedLimit, vy));

      // Bounce off walls
      const textWidth = 180;
      const textHeight = 80;
      if (x <= 0 || x + textWidth >= canvas.width) vx *= -1;
      if (y <= 20 || y + textHeight >= canvas.height) vy *= -1;
      
      // Ensure it stays within bounds
      x = Math.max(0, Math.min(x, canvas.width - textWidth));
      y = Math.max(20, Math.min(y, canvas.height - textHeight));

      const now = new Date();
      const timestamp = now.toLocaleString();

      ctx.font = '11px Inter, sans-serif';
      ctx.fillStyle = 'rgba(255, 255, 255, 0.22)';
      ctx.shadowColor = 'rgba(0, 0, 0, 0.4)';
      ctx.shadowBlur = 4;
      ctx.shadowOffsetX = 1;
      ctx.shadowOffsetY = 1;

      // Draw text
      const lines = [
        data.fullName,
        data.phone,
        `ID: ${data.userId.substring(0, 8)}`,
        `Session: ${data.sessionId}`,
        timestamp
      ];

      lines.forEach((line, index) => {
        ctx.fillText(line, x, y + (index * 16));
      });

      animationFrameId = requestAnimationFrame(render);
    };

    render();

    return () => {
      cancelAnimationFrame(animationFrameId);
    };
  }, [data]);

  return (
    <canvas
      ref={canvasRef}
      className={`absolute inset-0 w-full h-full pointer-events-none z-50 ${className}`}
      style={{ pointerEvents: 'none' }}
    />
  );
};
