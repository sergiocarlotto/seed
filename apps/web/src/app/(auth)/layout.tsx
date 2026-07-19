// Casca mínima centralizada, fora do shell (sem sidebar/topbar).
export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return <div className="flex min-h-full flex-col justify-center">{children}</div>;
}
