export function Logo() {
  return (
    <span className="flex items-center gap-2 font-semibold tracking-tight">
      <svg viewBox="0 0 32 32" className="size-7" aria-hidden>
        <rect width="32" height="32" rx="8" className="fill-primary" />
        <path d="M9 12h3v8H9zM20 12h3v8h-3zM12 15h8v2h-8zM6.5 14h2v4h-2zM23.5 14h2v4h-2z" className="fill-primary-fg" />
      </svg>
      <span>TrainerSphere</span>
    </span>
  );
}
