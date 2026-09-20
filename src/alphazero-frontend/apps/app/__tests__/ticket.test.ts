import { describe, expect, it } from "vitest";
import { createKeyTicket, verifyKeyTicket } from "../lib/player/ticket";

describe("Player Key Ticket Utility", () => {
  const videoId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
  const userId = "4e6f43e2-8924-4f01-9a71-6c2cb4a0558b";
  const tenantId = "b9f27091-64d1-419b-a36c-94038a8e3291";

  it("should create a validly formatted and verifiable key ticket", () => {
    const ticket = createKeyTicket(videoId, userId, tenantId);
    expect(ticket).toContain(".");

    const parts = ticket.split(".");
    expect(parts).toHaveLength(2);

    const result = verifyKeyTicket(ticket);
    expect(result.isValid).toBe(true);
    expect(result.payload?.vid).toBe(videoId);
    expect(result.payload?.uid).toBe(userId);
    expect(result.payload?.tid).toBe(tenantId);
    expect(result.payload?.exp).toBeGreaterThan(Math.floor(Date.now() / 1000));
  });

  it("should reject tampered ticket payload", () => {
    const ticket = createKeyTicket(videoId, userId, tenantId);
    const [b64, sig] = ticket.split(".");

    // Change a character in base64url payload
    const tamperedB64 = b64.slice(0, -2) + "==";
    const tamperedTicket = `${tamperedB64}.${sig}`;

    const result = verifyKeyTicket(tamperedTicket);
    expect(result.isValid).toBe(false);
  });

  it("should reject tampered ticket signature", () => {
    const ticket = createKeyTicket(videoId, userId, tenantId);
    const [b64, sig] = ticket.split(".");

    // Invert the last hex digit of the signature
    const lastChar = sig.slice(-1);
    const tamperedLastChar = lastChar === "0" ? "1" : "0";
    const tamperedSig = sig.slice(0, -1) + tamperedLastChar;
    const tamperedTicket = `${b64}.${tamperedSig}`;

    const result = verifyKeyTicket(tamperedTicket);
    expect(result.isValid).toBe(false);
    expect(result.error).toBe("Signature mismatch");
  });

  it("should reject malformed ticket strings", () => {
    expect(verifyKeyTicket("").isValid).toBe(false);
    expect(verifyKeyTicket("invalid-no-dot").isValid).toBe(false);
    expect(verifyKeyTicket(".only-dot").isValid).toBe(false);
  });
});
