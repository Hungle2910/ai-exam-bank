import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "AI Exam Bank — Hệ thống ngân hàng câu hỏi thông minh",
  description:
    "Nền tảng quản lý ngân hàng câu hỏi và tạo đề thi tự động với Amazon Bedrock AI. Dành cho giáo viên, trưởng bộ môn và quản trị trường học.",
  keywords: "ngân hàng câu hỏi, tạo đề thi, AI, Amazon Bedrock, giáo dục",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="vi">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
      </head>
      <body>{children}</body>
    </html>
  );
}
