import type { TenantRole } from '~/repositories/types'

const roleOrder: Record<TenantRole, number> = {
  Guest: 0,
  Member: 1,
  Coordinator: 2,
  Manager: 3,
  Owner: 4,
}

export function hasMinimumTenantRole(
  role: TenantRole | null,
  minimum: TenantRole,
  isAppAdmin: boolean,
): boolean {
  if (isAppAdmin) return true
  if (!role) return false
  return roleOrder[role] >= roleOrder[minimum]
}
