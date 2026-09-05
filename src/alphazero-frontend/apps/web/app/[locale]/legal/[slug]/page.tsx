import { ArrowLeftIcon } from "@radix-ui/react-icons";
import { createMetadata } from "@repo/seo/metadata";
import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";

interface LegalPageProperties {
  readonly params: Promise<{
    slug: string;
  }>;
}

const LEGAL_DOCS: Record<
  string,
  { title: string; description: string; content: string }
> = {
  privacy: {
    title: "Privacy Policy",
    description: "Our commitment to protecting your privacy and security.",
    content:
      "AlphaZero Academy respects your privacy. We are committed to protecting the personal information you share with us.",
  },
  terms: {
    title: "Terms of Service",
    description: "Terms and conditions governing the use of AlphaZero Academy.",
    content:
      "By accessing and using AlphaZero Academy, you agree to comply with our terms and applicable laws.",
  },
};

export const generateMetadata = async ({
  params,
}: LegalPageProperties): Promise<Metadata> => {
  const { slug } = await params;
  const doc = LEGAL_DOCS[slug];

  if (!doc) {
    return {};
  }

  return createMetadata({
    title: doc.title,
    description: doc.description,
  });
};

export const generateStaticParams = async (): Promise<{ slug: string }[]> => {
  return Object.keys(LEGAL_DOCS).map((slug) => ({ slug }));
};

const LegalPage = async ({ params }: LegalPageProperties) => {
  const { slug } = await params;
  const doc = LEGAL_DOCS[slug];

  if (!doc) {
    notFound();
  }

  return (
    <div className="container max-w-5xl py-16">
      <Link
        className="mb-4 inline-flex items-center gap-1 text-muted-foreground text-sm focus:underline focus:outline-none"
        href="/"
      >
        <ArrowLeftIcon className="h-4 w-4" />
        Back to Home
      </Link>
      <h1 className="scroll-m-20 text-balance font-extrabold text-4xl tracking-tight lg:text-5xl">
        {doc.title}
      </h1>
      <p className="text-balance leading-7 [&:not(:first-child)]:mt-6">
        {doc.description}
      </p>
      <div className="prose prose-neutral dark:prose-invert mt-16">
        <p>{doc.content}</p>
      </div>
    </div>
  );
};

export default LegalPage;
