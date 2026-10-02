import type { Metadata } from "next";
import { Header } from "./components/header";

const title = "AlphaZero Academy";
const description = "Online Academy Management Platform.";

export const metadata: Metadata = {
  title,
  description,
};

import Link from "next/link";
import { SparklesIcon, ArrowRightIcon } from "lucide-react";

const App = async () => {
  const pages = [
    { id: "1", name: "Courses & Curricula" },
    { id: "2", name: "Students & Enrollments" },
    { id: "3", name: "Library Codes & Redemptions" },
  ];

  return (
    <>
      <Header page="Dashboard" pages={["Academy Overview"]} />
      <div className="flex flex-1 flex-col gap-4 p-4 pt-0">
        {/* Component Showcase Banner */}
        <Link
          href="/components-showcase"
          className="flex items-center justify-between p-4 rounded-xl border border-primary/30 bg-primary/5 hover:bg-primary/10 transition-colors group cursor-pointer"
        >
          <div className="flex items-center gap-3">
            <span className="p-2 rounded-lg bg-primary/20 text-primary">
              <SparklesIcon className="size-5" />
            </span>
            <div>
              <h3 className="font-semibold text-sm text-foreground group-hover:text-primary transition-colors">
                LMS Domain Components Showcase
              </h3>
              <p className="text-xs text-muted-foreground">
                Interactive laboratory for BitmaskProgressBar, VoucherInput, and CourseCard with RTL/LTR bilingual testing.
              </p>
            </div>
          </div>
          <span className="flex items-center gap-1 text-xs font-semibold text-primary">
            Open Laboratory <ArrowRightIcon className="size-3.5 transition-transform group-hover:translate-x-0.5" />
          </span>
        </Link>

        <div className="grid auto-rows-min gap-4 md:grid-cols-3">
          {pages.map((page) => (
            <div
              className="flex aspect-video items-center justify-center rounded-xl bg-muted/50 p-4 font-medium"
              key={page.id}
            >
              {page.name}
            </div>
          ))}
        </div>
        <div className="min-h-[100vh] flex-1 rounded-xl bg-muted/50 md:min-h-min" />
      </div>
    </>
  );
};

export default App;
