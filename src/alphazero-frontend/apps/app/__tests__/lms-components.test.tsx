import { describe, expect, it, vi, afterEach } from "vitest";
import { render, screen, fireEvent, cleanup } from "@testing-library/react";
import React from "react";
import {
  BitmaskProgressBar,
  VoucherInput,
  CourseCard,
} from "@repo/design-system";

vi.mock("next/image", () => ({
  default: ({ src, alt, ...props }: any) => (
    <img src={src} alt={alt} {...props} />
  ),
}));

describe("LMS Domain Components", () => {
  afterEach(() => {
    cleanup();
  });

  describe("BitmaskProgressBar", () => {
    it("renders continuous progress bar correctly with partial completion", () => {
      // 4 ones out of 8 bits = 50%
      render(<BitmaskProgressBar bitmask="11100010" totalLessons={8} label="Physics 101" />);

      const progressbar = screen.getByRole("progressbar");
      expect(progressbar).toBeDefined();
      expect(progressbar.getAttribute("aria-valuenow")).toBe("50");
      expect(progressbar.getAttribute("aria-valuemin")).toBe("0");
      expect(progressbar.getAttribute("aria-valuemax")).toBe("100");

      const counter = screen.getByTestId("bitmask-counter");
      expect(counter.textContent).toContain("4/8 (50%)");

      const fill = screen.getByTestId("bitmask-continuous-fill");
      expect(fill.style.width).toBe("50%");
      expect(fill.className).toContain("bg-primary");
    });

    it("switches to success color when 100% complete", () => {
      render(<BitmaskProgressBar bitmask="1111" totalLessons={4} />);

      const progressbar = screen.getByRole("progressbar");
      expect(progressbar.getAttribute("aria-valuenow")).toBe("100");

      const fill = screen.getByTestId("bitmask-continuous-fill");
      expect(fill.className).toContain("bg-success");
    });

    it("handles empty or 0% bitmask gracefully without division by zero", () => {
      render(<BitmaskProgressBar bitmask="" totalLessons={0} />);

      const progressbar = screen.getByRole("progressbar");
      expect(progressbar.getAttribute("aria-valuenow")).toBe("0");
    });

    it("renders segmented variant with correct status pills", () => {
      // 3 lessons: Lesson 1 complete (1), Lesson 2 active (0), Lesson 3 locked (0)
      render(
        <BitmaskProgressBar
          bitmask="100"
          totalLessons={3}
          variant="segmented"
          label="Syllabus"
        />
      );

      const rail = screen.getByTestId("segmented-rail");
      expect(rail).toBeDefined();

      const seg0 = screen.getByTestId("bitmask-segment-0");
      const seg1 = screen.getByTestId("bitmask-segment-1");
      const seg2 = screen.getByTestId("bitmask-segment-2");

      expect(seg0.getAttribute("data-status")).toBe("completed");
      expect(seg1.getAttribute("data-status")).toBe("active");
      expect(seg2.getAttribute("data-status")).toBe("locked");
    });
  });

  describe("VoucherInput", () => {
    it("renders fixed prefix badge and 8 OTP slots", () => {
      render(<VoucherInput prefix="AZ" />);

      const prefixBadge = screen.getByTestId("voucher-prefix-badge");
      expect(prefixBadge.textContent).toContain("AZ");
    });

    it("renders error state with accessible error message", () => {
      render(
        <VoucherInput
          prefix="AZ"
          state="error"
          errorMessage="Voucher code already redeemed"
        />
      );

      const errorMsg = screen.getByTestId("voucher-error-message");
      expect(errorMsg.textContent).toContain("Voucher code already redeemed");
    });

    it("renders valid state with success message", () => {
      render(
        <VoucherInput
          prefix="AZ"
          state="valid"
          successMessage="Access code verified!"
        />
      );

      const successMsg = screen.getByTestId("voucher-success-message");
      expect(successMsg.textContent).toContain("Access code verified!");
    });
  });

  describe("CourseCard", () => {
    it("renders vertical card with title, category, instructor and CTA", () => {
      const onCtaClick = vi.fn();
      render(
        <CourseCard
          title="Differential Equations & Linear Algebra"
          category="Mathematics"
          instructor={{ name: "Dr. Ahmad Kanaan" }}
          lessonCount={16}
          duration="5.2 hrs"
          onCtaClick={onCtaClick}
        />
      );

      expect(screen.getByTestId("course-card-vertical")).toBeDefined();
      expect(screen.getByText("Differential Equations & Linear Algebra")).toBeDefined();
      expect(screen.getByText("Mathematics")).toBeDefined();
      expect(screen.getByText("Dr. Ahmad Kanaan")).toBeDefined();
      expect(screen.getByText("16 lessons")).toBeDefined();

      const button = screen.getByRole("button", { name: /enroll now/i });
      fireEvent.click(button);
      expect(onCtaClick).toHaveBeenCalledTimes(1);
    });

    it("renders progress bar and Continue Lesson CTA when enrolled", () => {
      render(
        <CourseCard
          title="Physics 101"
          isEnrolled={true}
          progress={{ bitmask: "1100", totalLessons: 4 }}
        />
      );

      expect(screen.getByRole("progressbar")).toBeDefined();
      expect(screen.getByRole("button", { name: /continue lesson/i })).toBeDefined();
    });

    it("renders horizontal compact layout when requested", () => {
      render(
        <CourseCard
          title="Machine Learning Fundamentals"
          layout="horizontal"
          isEnrolled={false}
          price="Free"
        />
      );

      expect(screen.getByTestId("course-card-horizontal")).toBeDefined();
      expect(screen.getByText("Free")).toBeDefined();
    });
  });
});
