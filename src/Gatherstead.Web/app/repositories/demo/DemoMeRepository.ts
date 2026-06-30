import type { MeSummary } from '../types'
import type { IMeRepository } from '../interfaces'
import { DEMO_USER_DISPLAY_NAME, DEMO_USER_EXTERNAL_ID, DEMO_USER_ID } from './demoConstants'

// The demo has a single hard-coded user, so the editable display name lives in its own
// localStorage key (seeded from DEMO_USER_DISPLAY_NAME) rather than the shared DemoStore.
const DISPLAY_NAME_KEY = 'gs-demo-display-name'

function readDisplayName(): string {
  try {
    return localStorage.getItem(DISPLAY_NAME_KEY) ?? DEMO_USER_DISPLAY_NAME
  }
  catch {
    return DEMO_USER_DISPLAY_NAME
  }
}

export class DemoMeRepository implements IMeRepository {
  async getMe(): Promise<MeSummary> {
    return { userId: DEMO_USER_ID, email: DEMO_USER_EXTERNAL_ID, displayName: readDisplayName() }
  }

  async updateDisplayName(displayName: string): Promise<MeSummary> {
    const normalized = displayName.trim()
    localStorage.setItem(DISPLAY_NAME_KEY, normalized)
    return { userId: DEMO_USER_ID, email: DEMO_USER_EXTERNAL_ID, displayName: normalized }
  }
}
