import type { Metadata } from "next";
import { Header } from "../components/header";
import { ComponentsShowcaseClient } from "./components-showcase-client";

export const metadata: Metadata = {
  title: "Component Laboratory | AlphaZero Academy",
  description: "Live interactive inspection sandbox for AlphaZero LMS domain components.",
};

export default function ComponentsShowcasePage() {
  return (
    <>
      <Header page="LMS Components Laboratory" pages={["Design System", "Specimens"]} />
      <div className="flex-1 overflow-auto">
        <ComponentsShowcaseClient />
      </div>
    </>
  );
}
