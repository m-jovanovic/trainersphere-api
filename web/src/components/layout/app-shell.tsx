import { Link, Outlet, useNavigate, useRouter } from '@tanstack/react-router';
import { useQueryClient } from '@tanstack/react-query';
import {
  CalendarDays,
  ChartLine,
  Dumbbell,
  History,
  House,
  LayoutGrid,
  ListChecks,
  LogOut,
  Menu as MenuIcon,
  Moon,
  Settings,
  Sun,
  SunMoon,
  Users,
  type LucideIcon,
} from 'lucide-react';
import type { ReactNode } from 'react';
import type { Role } from '@/lib/api/types';
import { useUser } from '@/lib/auth/hooks';
import { logout } from '@/features/auth/logout';
import { useTheme, type ThemePref } from '@/lib/theme';
import { fullName } from '@/lib/domain';
import { cn } from '@/lib/utils';
import { Avatar } from '@/components/ui/misc';
import { Button } from '@/components/ui/button';
import { Menu, MenuItem, MenuLabel, MenuSeparator } from '@/components/ui/menu';
import { SyncStatus } from './sync-status';
import { Logo } from './logo';

interface NavItem {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Shown in the phone bottom bar. */
  primary?: boolean;
}

const nav: Record<Role, NavItem[]> = {
  trainer: [
    { to: '/', label: 'Dashboard', icon: House, primary: true },
    { to: '/clients', label: 'Clients', icon: Users, primary: true },
    { to: '/schedule', label: 'Schedule', icon: CalendarDays, primary: true },
    { to: '/workouts', label: 'Workouts', icon: ListChecks, primary: true },
    { to: '/programs', label: 'Programs', icon: LayoutGrid },
    { to: '/exercises', label: 'Exercises', icon: Dumbbell },
  ],
  client: [
    { to: '/', label: 'Today', icon: House, primary: true },
    { to: '/history', label: 'History', icon: History, primary: true },
    { to: '/progress', label: 'Progress', icon: ChartLine, primary: true },
    { to: '/exercises', label: 'Exercises', icon: Dumbbell, primary: true },
  ],
};

const themeIcon: Record<ThemePref, LucideIcon> = { light: Sun, dark: Moon, system: SunMoon };
const nextTheme: Record<ThemePref, ThemePref> = { system: 'light', light: 'dark', dark: 'system' };

export function AppShell() {
  const user = useUser();
  const items = nav[user.role];
  return (
    <div className="min-h-dvh lg:grid lg:grid-cols-[15rem_1fr]">
      <a href="#main" className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-50 focus:rounded-lg focus:bg-surface focus:px-3 focus:py-2">
        Skip to content
      </a>
      {/* The column wrapper carries the background so it spans the whole page height. */}
      <div className="hidden border-r border-border bg-surface lg:block">
      <aside className="sticky top-0 flex h-dvh flex-col">
        <div className="flex h-16 items-center px-5">
          <Link to="/" aria-label="TrainerSphere home">
            <Logo />
          </Link>
        </div>
        <nav aria-label="Main" className="flex flex-1 flex-col gap-0.5 px-3">
          {items.map((item) => (
            <SideLink key={item.to} item={item} />
          ))}
        </nav>
        <div className="flex flex-col gap-2 border-t border-border p-3">
          <SyncStatus />
          <UserMenu align="start">
            <button type="button" className="flex w-full items-center gap-3 rounded-lg p-2 text-left hover:bg-surface-2">
              <Avatar name={user} />
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-medium">{fullName(user)}</span>
                <span className="block truncate text-xs text-muted capitalize">{user.role}</span>
              </span>
            </button>
          </UserMenu>
        </div>
      </aside>
      </div>

      <div className="flex min-w-0 flex-col">
        <header className="sticky top-0 z-30 flex h-14 items-center justify-between gap-3 border-b border-border bg-bg/90 px-4 backdrop-blur lg:hidden">
          <Link to="/" aria-label="TrainerSphere home">
            <Logo />
          </Link>
          <div className="flex items-center gap-2">
            <SyncStatus compact />
            <UserMenu>
              <Button variant="ghost" size="icon" aria-label="Account menu">
                <Avatar name={user} className="size-8" />
              </Button>
            </UserMenu>
          </div>
        </header>
        <main id="main" className="mx-auto w-full max-w-6xl flex-1 px-4 pt-5 pb-[calc(7rem+env(safe-area-inset-bottom))] sm:px-6 lg:px-8 lg:pt-8 lg:pb-12">
          <Outlet />
        </main>
        <BottomNav items={items} />
      </div>
    </div>
  );
}

function SideLink({ item }: { item: NavItem }) {
  const Icon = item.icon;
  return (
    <Link
      to={item.to}
      activeOptions={{ exact: item.to === '/' }}
      className="flex h-10 items-center gap-3 rounded-lg px-3 text-sm font-medium text-muted transition-colors hover:bg-surface-2 hover:text-fg data-[status=active]:bg-primary-soft data-[status=active]:text-primary-soft-fg"
    >
      <Icon className="size-4.5" aria-hidden />
      {item.label}
    </Link>
  );
}

function BottomNav({ items }: { items: NavItem[] }) {
  const primary = items.filter((i) => i.primary);
  const more = items.filter((i) => !i.primary);
  const navigate = useNavigate();
  const cell = 'flex flex-1 flex-col items-center justify-center gap-0.5 text-[11px] font-medium text-muted data-[status=active]:text-primary min-h-14';
  return (
    <nav aria-label="Main" className="fixed inset-x-0 bottom-0 z-30 border-t border-border bg-surface/95 pb-safe backdrop-blur lg:hidden">
      <div className="mx-auto flex max-w-lg">
        {primary.map((item) => {
          const Icon = item.icon;
          return (
            <Link key={item.to} to={item.to} activeOptions={{ exact: item.to === '/' }} className={cell}>
              <Icon className="size-5.5" aria-hidden />
              {item.label}
            </Link>
          );
        })}
        {more.length > 0 && (
          <Menu
            trigger={
              <button type="button" className={cell}>
                <MenuIcon className="size-5.5" aria-hidden />
                More
              </button>
            }
          >
            {more.map((item) => {
              const Icon = item.icon;
              return (
                <MenuItem key={item.to} icon={<Icon />} onSelect={() => void navigate({ to: item.to })}>
                  {item.label}
                </MenuItem>
              );
            })}
          </Menu>
        )}
      </div>
    </nav>
  );
}

function UserMenu({ children, align = 'end' }: { children: ReactNode; align?: 'start' | 'end' }) {
  const user = useUser();
  const navigate = useNavigate();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [theme, setTheme] = useTheme();
  const ThemeIcon = themeIcon[theme];
  return (
    <Menu trigger={children} align={align}>
      <MenuLabel>{user.email}</MenuLabel>
      <MenuItem icon={<Settings />} onSelect={() => void navigate({ to: '/settings' })}>
        Settings
      </MenuItem>
      <MenuItem icon={<ThemeIcon />} onSelect={() => setTheme(nextTheme[theme])}>
        Theme: <span className="capitalize">{theme}</span>
      </MenuItem>
      <MenuSeparator />
      <MenuItem
        icon={<LogOut />}
        onSelect={() => {
          void logout(queryClient).then(() => router.navigate({ to: '/login' }));
        }}
      >
        Log out
      </MenuItem>
    </Menu>
  );
}

export function NavBadge({ children, className }: { children: ReactNode; className?: string }) {
  return <span className={cn('ml-auto rounded-full bg-surface-3 px-1.5 text-xs', className)}>{children}</span>;
}
