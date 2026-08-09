
export function SayfaDugmesi({
  onClick,
  devreDisi,
  etiket,
  children,
}: {
  onClick: () => void;
  devreDisi: boolean;
  etiket: string;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={devreDisi}
      aria-label={etiket}
      className="rounded-md border border-outline p-base text-on-surface transition-colors hover:bg-surface-container-high disabled:opacity-40"
    >
      {children}
    </button>
  );
}

export function Baslik({ children }: { children: React.ReactNode }) {
  return (
    <th className="px-stack-sm py-base font-body text-[10px] font-bold uppercase tracking-[0.14em] text-on-surface-variant">
      {children}
    </th>
  );
}

export function Hucre({ children }: { children: React.ReactNode }) {
  return <td className="px-stack-sm py-stack-sm align-top font-body text-body-sm">{children}</td>;
}
