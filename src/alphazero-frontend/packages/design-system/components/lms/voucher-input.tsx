"use client";

import * as React from "react";
import {
  InputOTP,
  InputOTPGroup,
  InputOTPSlot,
} from "@repo/design-system/components/ui/input-otp";
import { AlertCircleIcon, CheckCircle2Icon, Loader2Icon, TicketIcon } from "lucide-react";
import { cn } from "@repo/design-system/lib/utils";

export interface VoucherInputProps {
  /**
   * The current 8-character PIN value (without prefix).
   */
  value?: string;
  /**
   * Callback fired when the PIN input changes.
   * Returns the raw 8-character uppercase PIN.
   */
  onChange?: (value: string) => void;
  /**
   * Callback fired when all 8 characters are entered.
   * Passes the full normalized code format (e.g. "AZ-9482-K92X").
   */
  onComplete?: (fullCode: string) => void;
  /**
   * Static voucher code prefix badge.
   * @default "AZ"
   */
  prefix?: string;
  /**
   * Validation state of the voucher:
   * - "idle": Standard input state.
   * - "valid": Green verified state with check icon.
   * - "error": Red invalid state with error message.
   * - "loading": Async validating/submitting state.
   * @default "idle"
   */
  state?: "idle" | "valid" | "error" | "loading";
  /**
   * Descriptive error message shown when state === "error".
   */
  errorMessage?: string;
  /**
   * Descriptive success message shown when state === "valid".
   */
  successMessage?: string;
  /**
   * Whether the input is disabled.
   * @default false
   */
  disabled?: boolean;
  /**
   * Auto focus the first slot on mount.
   * @default false
   */
  autoFocus?: boolean;
  /**
   * Optional custom container CSS classes.
   */
  className?: string;
}

export function VoucherInput({
  value: controlledValue,
  onChange,
  onComplete,
  prefix = "AZ",
  state = "idle",
  errorMessage,
  successMessage,
  disabled = false,
  autoFocus = false,
  className,
}: VoucherInputProps) {
  const [internalValue, setInternalValue] = React.useState("");
  const isControlled = controlledValue !== undefined;
  const pinValue = isControlled ? controlledValue : internalValue;

  // Clean and normalize input (strips prefix and dashes on paste)
  const cleanInput = React.useCallback(
    (raw: string) => {
      let cleaned = raw.toUpperCase().replace(/[\s-]/g, "");
      // If user pasted code with the prefix, strip the prefix
      if (prefix && cleaned.startsWith(prefix.toUpperCase())) {
        cleaned = cleaned.slice(prefix.length);
      }
      // Limit to 8 alphanumeric characters
      return cleaned.replace(/[^A-Z0-9]/g, "").slice(0, 8);
    },
    [prefix]
  );

  const handleChange = (incoming: string) => {
    const cleaned = cleanInput(incoming);
    if (!isControlled) {
      setInternalValue(cleaned);
    }
    onChange?.(cleaned);

    if (cleaned.length === 8) {
      const fullCode = `${prefix}-${cleaned.slice(0, 4)}-${cleaned.slice(4, 8)}`;
      onComplete?.(fullCode);
    }
  };

  const isError = state === "error";
  const isValid = state === "valid";
  const isLoading = state === "loading";

  return (
    <div className={cn("flex flex-col items-center gap-3", className)}>
      <div
        className={cn(
          "flex items-center gap-2 p-2 rounded-xl transition-all border",
          isError && "border-destructive/60 bg-destructive/5 ring-1 ring-destructive/20",
          isValid && "border-success/60 bg-success/5 ring-1 ring-success/20",
          !isError && !isValid && "border-border/60 bg-card/40 hover:border-border"
        )}
      >
        {/* Fixed Prefix Badge */}
        <div
          data-testid="voucher-prefix-badge"
          className={cn(
            "flex items-center gap-1.5 px-3 py-2 rounded-lg border text-sm font-mono font-bold select-none transition-colors",
            isValid
              ? "bg-success/15 border-success/30 text-success"
              : isError
              ? "bg-destructive/15 border-destructive/30 text-destructive"
              : "bg-muted/70 border-border text-foreground/80"
          )}
        >
          <TicketIcon className="size-3.5 opacity-70" />
          <span>{prefix}</span>
        </div>

        <span className="text-muted-foreground font-mono font-semibold select-none text-xs">
          —
        </span>

        {/* 8-Character OTP Input (2 groups of 4 slots) */}
        <InputOTP
          maxLength={8}
          pattern="^[a-zA-Z0-9]+$"
          value={pinValue}
          onChange={handleChange}
          disabled={disabled || isLoading}
          autoFocus={autoFocus}
          aria-invalid={isError}
          data-testid="voucher-otp-input"
          containerClassName="gap-2"
        >
          {/* Group 1 (Slots 0..3) */}
          <InputOTPGroup className="gap-1">
            <InputOTPSlot
              index={0}
              className={cn(
                "size-10 font-mono text-base uppercase rounded-md border",
                isValid && "border-success/40 text-success font-bold",
                isError && "border-destructive/40 text-destructive"
              )}
            />
            <InputOTPSlot
              index={1}
              className={cn(
                "size-10 font-mono text-base uppercase rounded-md border",
                isValid && "border-success/40 text-success font-bold",
                isError && "border-destructive/40 text-destructive"
              )}
            />
            <InputOTPSlot
              index={2}
              className={cn(
                "size-10 font-mono text-base uppercase rounded-md border",
                isValid && "border-success/40 text-success font-bold",
                isError && "border-destructive/40 text-destructive"
              )}
            />
            <InputOTPSlot
              index={3}
              className={cn(
                "size-10 font-mono text-base uppercase rounded-md border",
                isValid && "border-success/40 text-success font-bold",
                isError && "border-destructive/40 text-destructive"
              )}
            />
          </InputOTPGroup>

          <span className="text-muted-foreground font-mono font-semibold select-none text-xs">
            —
          </span>

          {/* Group 2 (Slots 4..7) */}
          <InputOTPGroup className="gap-1">
            <InputOTPSlot
              index={4}
              className={cn(
                "size-10 font-mono text-base uppercase rounded-md border",
                isValid && "border-success/40 text-success font-bold",
                isError && "border-destructive/40 text-destructive"
              )}
            />
            <InputOTPSlot
              index={5}
              className={cn(
                "size-10 font-mono text-base uppercase rounded-md border",
                isValid && "border-success/40 text-success font-bold",
                isError && "border-destructive/40 text-destructive"
              )}
            />
            <InputOTPSlot
              index={6}
              className={cn(
                "size-10 font-mono text-base uppercase rounded-md border",
                isValid && "border-success/40 text-success font-bold",
                isError && "border-destructive/40 text-destructive"
              )}
            />
            <InputOTPSlot
              index={7}
              className={cn(
                "size-10 font-mono text-base uppercase rounded-md border",
                isValid && "border-success/40 text-success font-bold",
                isError && "border-destructive/40 text-destructive"
              )}
            />
          </InputOTPGroup>
        </InputOTP>

        {/* State Indicator Icon */}
        {isLoading && (
          <Loader2Icon className="size-4 animate-spin text-primary ms-1" />
        )}
        {isValid && (
          <CheckCircle2Icon className="size-4 text-success ms-1 animate-in zoom-in-50" />
        )}
        {isError && (
          <AlertCircleIcon className="size-4 text-destructive ms-1 animate-in zoom-in-50" />
        )}
      </div>

      {/* Contextual Feedback Text */}
      {isError && errorMessage && (
        <p
          className="text-xs text-destructive flex items-center gap-1 animate-in fade-in-50"
          data-testid="voucher-error-message"
        >
          <AlertCircleIcon className="size-3 shrink-0" />
          <span>{errorMessage}</span>
        </p>
      )}

      {isValid && successMessage && (
        <p
          className="text-xs text-success flex items-center gap-1 animate-in fade-in-50"
          data-testid="voucher-success-message"
        >
          <CheckCircle2Icon className="size-3 shrink-0" />
          <span>{successMessage}</span>
        </p>
      )}
    </div>
  );
}
