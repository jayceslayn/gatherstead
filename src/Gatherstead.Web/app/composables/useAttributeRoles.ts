import type { TenantRole, AttributeEntry, AttributeWriteEntry } from '~/repositories/types'

// Maps a read-side attribute list (from an entity DTO) to the write shape used by forms.
export function toAttributeWriteEntries(attributes: AttributeEntry[] | null | undefined): AttributeWriteEntry[] {
  return (attributes ?? []).map(a => ({
    key: a.key,
    value: a.value,
    tenantMinRole: a.tenantMinRole,
    householdMinRole: a.householdMinRole,
  }))
}

// Drops blank rows (no label) before persisting.
export function cleanAttributeWriteEntries(rows: AttributeWriteEntry[]): AttributeWriteEntry[] {
  return rows.filter(a => (a.key ?? '').trim())
}

// A row that carries a value but no label would be silently dropped by
// cleanAttributeWriteEntries, losing the user's data. The editor flags such rows and forms
// block saving until the user adds a label or removes the row via the delete button.
export function isIncompleteAttributeRow(row: AttributeWriteEntry): boolean {
  return !(row.key ?? '').trim() && !!(row.value ?? '').trim()
}

export function hasIncompleteAttributeRows(rows: AttributeWriteEntry[]): boolean {
  return rows.some(isIncompleteAttributeRow)
}

// Numeric values mirror the backend TenantRole enum (Owner 0 … Guest 4). An attribute is
// visible to callers whose role is at least as privileged, i.e. callerRole <= tenantMinRole.
export const TENANT_ROLE_VALUES: Record<TenantRole, number> = {
  Owner: 0,
  Manager: 1,
  Coordinator: 2,
  Member: 3,
  Guest: 4,
}

const TENANT_ROLE_BY_VALUE: Record<number, TenantRole> = {
  0: 'Owner',
  1: 'Manager',
  2: 'Coordinator',
  3: 'Member',
  4: 'Guest',
}

export function useAttributeRoles() {
  const { t } = useI18n()

  // Ordered most-open (Everyone) to most-restricted (Owners only) for the visibility dropdown.
  const order: TenantRole[] = ['Guest', 'Member', 'Coordinator', 'Manager', 'Owner']
  const roleItems = computed(() =>
    order.map(role => ({ label: t(`attribute.visibleTo.${role.toLowerCase()}`), value: TENANT_ROLE_VALUES[role] })),
  )

  function roleLabel(value: number | null | undefined): string {
    const role = TENANT_ROLE_BY_VALUE[value ?? TENANT_ROLE_VALUES.Member] ?? 'Member'
    return t(`attribute.visibleTo.${role.toLowerCase()}`)
  }

  return { roleItems, roleLabel }
}
