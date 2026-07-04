import type { BedSize } from '../types'

// Approximate sleeps per bed size, mirroring the backend BedSizes.Sleeps.
const BED_SLEEPS: Record<BedSize, number> = {
  Single: 1, Double: 2, Queen: 2, King: 2, Bunk: 2, Sofa: 1, Crib: 1, Other: 1,
}

// Total sleeps from bed inventory; null when no beds are recorded (unconstrained). Accepts the loose
// generated bed shape (optional fields) since stored accommodation beds are typed that way.
export function sleepsCapacity(beds: readonly { size?: BedSize, quantity?: number }[]): number | null {
  if (!beds.length) return null
  return beds.reduce((s, b) => s + (b.quantity ?? 0) * (b.size ? BED_SLEEPS[b.size] : 1), 0)
}

export function enumDays(startDate: string, endDate: string): string[] {
  const days: string[] = []
  const d = new Date(startDate)
  const end = new Date(endDate)
  while (d <= end) {
    days.push(d.toISOString().substring(0, 10))
    d.setDate(d.getDate() + 1)
  }
  return days
}
