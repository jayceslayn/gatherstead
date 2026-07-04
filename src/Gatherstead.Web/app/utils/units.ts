// Metric ↔ imperial conversions for accommodation dimensions. Dimensions are persisted in metres;
// feet and area are derived here for display only.

const SQ_METERS_TO_SQ_FEET = 10.7639

export function sqMetersToSqFeet(sqm: number): number {
  return sqm * SQ_METERS_TO_SQ_FEET
}

/** Area override wins over width × depth; null when neither is available. */
export function effectiveAreaSqMeters(
  width: number | null,
  depth: number | null,
  override: number | null,
): number | null {
  if (override != null) return override
  if (width != null && depth != null) return width * depth
  return null
}

/** e.g. "12 m² (129 ft²)". Returns empty string for null. */
export function formatArea(sqm: number | null): string {
  if (sqm == null) return ''
  return `${round(sqm)} m² (${round(sqMetersToSqFeet(sqm))} ft²)`
}

function round(n: number): number {
  return Math.round(n * 10) / 10
}
