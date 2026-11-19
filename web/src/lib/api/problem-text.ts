/** API JSON path (`exercises[0].sets`) to react-hook-form path (`exercises.0.sets`). */
export function toFieldPath(key: string): string {
  return key.replace(/\[(\d+)\]/g, '.$1');
}

/** `exercises[0].restSeconds` reads as "Exercise 1 rest seconds". */
export function describePath(key: string): string {
  const words = key
    .replace(/(\w+?)s?\[(\d+)\]/g, (_, name: string, i: string) => `${name} ${Number(i) + 1} `)
    .replace(/\./g, ' ')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

/**
 * A validation message shown away from its field. `$` means the API could not read the body
 * at all, so its message stands alone; other keys get their path so the message has a subject.
 */
export function formLevelMessage(key: string, message: string): string {
  return key === '$' || key === '' ? message : `${describePath(key)}: ${message}`;
}

/** Every validation error as one readable sentence each, duplicates removed. */
export function validationMessages(errors: Record<string, string[]> | undefined): string[] {
  const out = Object.entries(errors ?? {}).flatMap(([key, messages]) => (messages[0] ? [formLevelMessage(key, messages[0])] : []));
  return [...new Set(out)];
}
