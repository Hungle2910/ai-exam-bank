import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "AI Exam Bank: Ngân hàng câu hỏi & Ma trận đề thi",
  description:
    "Nền tảng quản lý ngân hàng câu hỏi, ma trận đặc tả và thẩm định đề thi dành cho giáo viên, trưởng bộ môn và quản trị trường học.",
  keywords: "ngân hàng câu hỏi, ma trận đề thi, thẩm định đề, giáo dục",
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
        <link
          rel="stylesheet"
          href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;500;600&family=Source+Sans+3:ital,wght@0,400;0,500;0,600;0,700;1,400&family=Spectral:ital,wght@0,500;0,600;0,700;1,400&display=swap"
        />
      </head>
      <body>{children}</body>
    </html>
  );
}
