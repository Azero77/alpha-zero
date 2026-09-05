import { auth } from "@repo/auth/server";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Header } from "./components/header";

const title = "AlphaZero Academy";
const description = "Online Academy Management Platform.";

export const metadata: Metadata = {
  title,
  description,
};

const App = async () => {
  const { orgId } = await auth();

  if (!orgId) {
    notFound();
  }

  const pages = [
    { id: "1", name: "Courses & Curricula" },
    { id: "2", name: "Students & Enrollments" },
    { id: "3", name: "Library Codes & Redemptions" },
  ];

  return (
    <>
      <Header page="Dashboard" pages={["Academy Overview"]} />
      <div className="flex flex-1 flex-col gap-4 p-4 pt-0">
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
