"use client";

export const SignUp = () => {
  return (
    <div className="flex w-full max-w-sm flex-col gap-6 rounded-lg border bg-card p-6 shadow-sm">
      <div className="flex flex-col gap-2 text-center">
        <h1 className="text-2xl font-bold tracking-tight">Register for AlphaZero</h1>
        <p className="text-sm text-muted-foreground">
          Create an account to join the academy
        </p>
      </div>
      <form className="flex flex-col gap-4">
        <div className="flex flex-col gap-2 text-start">
          <label className="text-sm font-medium" htmlFor="name">
            Full Name
          </label>
          <input
            id="name"
            name="name"
            type="text"
            required
            className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            placeholder="Ahmad Al-Ahmad"
          />
        </div>
        <div className="flex flex-col gap-2 text-start">
          <label className="text-sm font-medium" htmlFor="username">
            Username
          </label>
          <input
            id="username"
            name="username"
            type="text"
            required
            className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          />
        </div>
        <div className="flex flex-col gap-2 text-start">
          <label className="text-sm font-medium" htmlFor="password">
            Password
          </label>
          <input
            id="password"
            name="password"
            type="password"
            required
            className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          />
        </div>
        <button
          type="submit"
          className="inline-flex h-9 items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground shadow transition-colors hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
        >
          Create Account
        </button>
      </form>
    </div>
  );
};
