import type { AccommodationIntentStatus } from '~/repositories/types'

export const memberRequestStatuses = ['Requested'] as const satisfies readonly AccommodationIntentStatus[]

export function canEditAccommodationIntent(
  status: AccommodationIntentStatus,
  canManageStatus: boolean,
): boolean {
  return canManageStatus || status === 'Requested'
}
