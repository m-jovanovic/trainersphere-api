import { screen, within } from '@testing-library/react';

/** The Card that owns the level-2 heading `title`; CardHeader nests it two divs deep. */
export async function findCard(title: string | RegExp) {
  const heading = await screen.findByRole('heading', { name: title, level: 2 }, { timeout: 5000 });
  const card = heading.parentElement?.parentElement?.parentElement;
  if (!card) throw new Error(`No card around heading ${String(title)}`);
  return within(card);
}

export async function findDialog(name: string | RegExp) {
  return within(await screen.findByRole('dialog', { name }, { timeout: 5000 }));
}

/** Overrides matchMedia so `min-width` queries match, giving the desktop layout. */
export function useDesktopViewport() {
  const original = window.matchMedia;
  window.matchMedia = (query: string) =>
    ({
      matches: query.includes('min-width'),
      media: query,
      onchange: null,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }) as MediaQueryList;
  return () => {
    window.matchMedia = original;
  };
}
