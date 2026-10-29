import { describe, expect, it } from 'vitest'
import { canEditAccommodationIntent, memberRequestStatuses } from '../../app/utils/accommodationIntentPolicy'
import { hasMinimumTenantRole } from '../../app/utils/tenantRoles'

describe('accommodation intent policy', () => {
  it('offers Requested as the only member request status', () => {
    expect(memberRequestStatuses).toEqual(['Requested'])
  })

  it('allows non-Coordinator users to edit only Requested stays', () => {
    expect(canEditAccommodationIntent('Requested', false)).toBe(true)
    expect(canEditAccommodationIntent('Hold', false)).toBe(false)
    expect(canEditAccommodationIntent('Confirmed', false)).toBe(false)
    expect(canEditAccommodationIntent('Declined', false)).toBe(false)
  })

  it('allows Coordinator+ users to edit stays in any status', () => {
    expect(canEditAccommodationIntent('Hold', true)).toBe(true)
    expect(canEditAccommodationIntent('Confirmed', true)).toBe(true)
    expect(canEditAccommodationIntent('Declined', true)).toBe(true)
  })
})

describe('tenant role thresholds', () => {
  it('includes Coordinator and higher roles at the Coordinator threshold', () => {
    expect(hasMinimumTenantRole('Coordinator', 'Coordinator', false)).toBe(true)
    expect(hasMinimumTenantRole('Manager', 'Coordinator', false)).toBe(true)
    expect(hasMinimumTenantRole('Owner', 'Coordinator', false)).toBe(true)
    expect(hasMinimumTenantRole('Member', 'Coordinator', false)).toBe(false)
    expect(hasMinimumTenantRole('Guest', 'Coordinator', false)).toBe(false)
  })

  it('allows App Admins to satisfy every role threshold', () => {
    expect(hasMinimumTenantRole(null, 'Coordinator', true)).toBe(true)
    expect(hasMinimumTenantRole(null, 'Coordinator', false)).toBe(false)
  })
})
