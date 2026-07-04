import type { BedSize } from '~/repositories/types'

/**
 * Bed inventory summary, e.g. "2 Queen, 1 Bunk". Accepts the loose generated bed shape
 * (optional fields); entries without a size or quantity are skipped. Empty string when none.
 */
export function formatBedSummary(
  beds: readonly { size?: BedSize, quantity?: number }[] | null | undefined,
  t: (key: string) => string,
): string {
  return (beds ?? [])
    .filter(b => b.size && b.quantity)
    .map(b => `${b.quantity} ${t(`accommodation.bedSizes.${b.size!.toLowerCase()}`)}`)
    .join(', ')
}
