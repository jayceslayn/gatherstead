import { describe, expect, it } from 'vitest'
import { accommodationTypeIcon, accommodationTypeLabelKey } from '../../app/utils/accommodations'

describe('accommodationTypeIcon', () => {
  it('maps each known type to its icon', () => {
    expect(accommodationTypeIcon('Bedroom')).toBe('i-heroicons-home')
    expect(accommodationTypeIcon('Bunk')).toBe('i-heroicons-rectangle-stack')
    expect(accommodationTypeIcon('RvPad')).toBe('i-heroicons-truck')
    expect(accommodationTypeIcon('Tent')).toBe('i-heroicons-map')
    expect(accommodationTypeIcon('Offsite')).toBe('i-heroicons-arrow-top-right-on-square')
  })

  it('falls back to the bedroom icon for unknown values', () => {
    expect(accommodationTypeIcon('Yurt')).toBe('i-heroicons-home')
  })
})

describe('accommodationTypeLabelKey', () => {
  it('camel-cases the PascalCase type under accommodation.types', () => {
    expect(accommodationTypeLabelKey('Bedroom')).toBe('accommodation.types.bedroom')
    expect(accommodationTypeLabelKey('RvPad')).toBe('accommodation.types.rvPad')
  })
})
