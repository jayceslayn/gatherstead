import type { AccommodationType } from '~/repositories/types'

const TYPE_ICONS: Record<AccommodationType, string> = {
  Bedroom: 'i-heroicons-home',
  Bunk: 'i-heroicons-rectangle-stack',
  RvPad: 'i-heroicons-truck',
  Tent: 'i-heroicons-map',
  Offsite: 'i-heroicons-arrow-top-right-on-square',
}

/** Icon for an accommodation type, falling back to the bedroom icon for unknown values. */
export function accommodationTypeIcon(type: string): string {
  return TYPE_ICONS[type as AccommodationType] ?? 'i-heroicons-home'
}

/** i18n key for an accommodation type label (`accommodation.types.*`, PascalCase → camelCase). */
export function accommodationTypeLabelKey(type: string): string {
  return `accommodation.types.${type.charAt(0).toLowerCase() + type.slice(1)}`
}
