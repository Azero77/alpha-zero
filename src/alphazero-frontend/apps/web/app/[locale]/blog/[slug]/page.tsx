import { createMetadata } from "@repo/seo/metadata";
import type { Metadata } from "next";
import { notFound } from "next/navigation";

interface BlogPostProperties {
  readonly params: Promise<{
    slug: string;
  }>;
}

export const generateMetadata = async ({
  params,
}: BlogPostProperties): Promise<Metadata> => {
  const { slug } = await params;

  return createMetadata({
    title: `${slug} - Blog`,
    description: "AlphaZero Academy Blog Post",
  });
};

export const generateStaticParams = async (): Promise<{ slug: string }[]> => {
  return [];
};

const BlogPost = () => {
  notFound();
};

export default BlogPost;
