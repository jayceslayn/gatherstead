// Legal document model.
//
// NOTE ON i18n: the app rule is "no hardcoded strings," but the Terms of Service and Privacy
// Policy are deliberately kept English-only and authoritative (see the "English version governs"
// notice rendered on each page). Translating legal text machine-style is legally risky, so the long
// prose lives here rather than in i18n/locales/*.json. Only the short UI chrome around it (page
// titles, the consent line, contact link) is localized.
//
// NOTE ON IDENTITY: no deployment-specific legal identity (provider name, jurisdiction, contact
// address) is hardcoded. Those are supplied at render time from runtime config via `LegalContext`,
// so a fork of this open repo automatically reflects its own operator. See `resolveLegalContext`.

/** The "last updated" / effective date shown on both documents. Bump on material change. */
export const LEGAL_LAST_UPDATED = 'July 12, 2026'

/** Deployment-specific values interpolated into the legal prose at render time. */
export interface LegalContext {
  /** The contracting party ("Provider"). Falls back to a neutral phrase when unset. */
  providerName: string
  /** Governing-law jurisdiction as configured (e.g. "Oregon"); empty when unset. */
  jurisdiction: string
  /** Support/legal contact address; empty when unset (page shows a neutral fallback). */
  contactEmail: string
  /** Product name, from i18n `common.appName`. */
  appName: string
  /** Effective date string. */
  effectiveDate: string
}

export interface LegalBlock {
  type: 'p' | 'list' | 'contact'
  /** For `p`. */
  text?: string
  /** For `list`. */
  items?: string[]
}

export interface LegalSection {
  id: string
  heading: string
  blocks: LegalBlock[]
}

export interface LegalDoc {
  title: string
  intro: string
  effectiveDate: string
  /** Passed through so the `contact` block can render a mailto link or fall back to /contact. */
  contactEmail: string
  sections: LegalSection[]
}

/**
 * Build a `LegalContext` from the public runtime config, applying neutral fallbacks so the pages
 * render sensibly even when a deployment (or a fork) has set none of the legal env values.
 */
export function resolveLegalContext(
  publicConfig: { legalProvider?: string; legalJurisdiction?: string; contactEmail?: string },
  appName: string,
): LegalContext {
  const provider = (publicConfig.legalProvider ?? '').trim()
  return {
    providerName: provider || `the operator of ${appName}`,
    jurisdiction: (publicConfig.legalJurisdiction ?? '').trim(),
    contactEmail: (publicConfig.contactEmail ?? '').trim(),
    appName,
    effectiveDate: LEGAL_LAST_UPDATED,
  }
}
