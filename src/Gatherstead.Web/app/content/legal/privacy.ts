import type { LegalContext, LegalDoc } from './types'

/**
 * Privacy Policy — English-only authoritative template (see the note in `types.ts`).
 * Tailored to this stack: US-hosted Azure, cookieless analytics, Microsoft sub-processors, and a firm
 * no-sale/no-share stance. A starting point, not legal advice — have it reviewed by counsel.
 */
export function buildPrivacyDoc(ctx: LegalContext): LegalDoc {
  const { providerName, contactEmail, appName, effectiveDate } = ctx

  return {
    title: 'Privacy Policy',
    effectiveDate,
    contactEmail,
    intro:
      `This Privacy Policy explains how ${providerName} ("we," "us," or the "Provider") collects, uses, and ` +
      `protects personal information in connection with ${appName} (the "Service"). We designed the Service to ` +
      `keep each household's data private, and we treat personal information as sensitive by default.`,
    sections: [
      {
        id: 'collect',
        heading: 'Information we collect',
        blocks: [
          { type: 'p', text: `We collect the following categories of information:` },
          {
            type: 'list',
            items: [
              `Account information: your email address and display name, provided through our identity provider when you sign in.`,
              `Household and member information you enter: names, birth dates or age bands, contact methods, addresses, dietary and medical notes, relationships, and related notes for the people in your groups.`,
              `Activity information: event attendance, meal and task sign-ups, accommodation and shopping intents, and similar coordination data you create in the Service.`,
              `Technical information: limited, privacy-safe diagnostic and performance telemetry (see "Cookies and analytics"), and security audit logs that record events such as failed sign-ins using only salted-hash network identifiers. We never store your raw IP address.`,
            ],
          },
        ],
      },
      {
        id: 'use',
        heading: 'How we use information',
        blocks: [
          {
            type: 'p',
            text:
              `We use personal information solely to operate and provide the Service — for example, to ` +
              `authenticate you, maintain your groups and households, coordinate events, and keep the Service ` +
              `secure and reliable. We do not use your personal information for advertising or profiling.`,
          },
        ],
      },
      {
        id: 'no-sale',
        heading: 'We do not sell or share your information',
        blocks: [
          {
            type: 'p',
            text:
              `We do not sell, rent, or trade your personal information, and we do not share it with third parties ` +
              `for their own purposes. This is true today and will remain true under any future paid or ` +
              `subscription model. We share information only with the service providers that operate our ` +
              `infrastructure, as described below, and only as needed to run the Service.`,
          },
        ],
      },
      {
        id: 'subprocessors',
        heading: 'Service providers',
        blocks: [
          {
            type: 'p',
            text:
              `We rely on Microsoft Azure to run the Service, and our data is hosted in the United States. These ` +
              `providers process data on our behalf under their contractual and security commitments:`,
          },
          {
            type: 'list',
            items: [
              `Microsoft Azure — cloud hosting, database, and key management (United States).`,
              `Microsoft Azure Application Insights / Log Analytics — privacy-safe diagnostics and performance monitoring.`,
              `Microsoft Entra External ID — authentication and account sign-in.`,
            ],
          },
        ],
      },
      {
        id: 'cookies',
        heading: 'Cookies and analytics',
        blocks: [
          {
            type: 'p',
            text:
              `Our analytics are cookieless and are designed never to receive names, emails, notes, or other ` +
              `personal details — only opaque identifiers, counts, and coarse metadata. Because of this, we do not ` +
              `display a cookie consent banner.`,
          },
          {
            type: 'p',
            text:
              `The Service uses only strictly necessary, first-party cookies: one to keep you signed in (your ` +
              `session) and one to remember your language preference. Our identity provider may also set its own ` +
              `cookie during sign-in to manage your authentication session.`,
          },
        ],
      },
      {
        id: 'children',
        heading: "Children's data",
        blocks: [
          {
            type: 'p',
            text:
              `The Service is intended for adults. Account holders may enter information about their family ` +
              `members, which can include minors. If you enter information about a child or any other person, you ` +
              `are responsible for that information and confirm you have the authority to provide it.`,
          },
          {
            type: 'p',
            text:
              `We do not knowingly allow children to create their own accounts. If you believe a child has created ` +
              `an account, please contact us so we can address it.`,
          },
        ],
      },
      {
        id: 'security',
        heading: 'How we protect information',
        blocks: [
          {
            type: 'p',
            text:
              `We use industry-standard safeguards, including encryption in transit (TLS) and encryption at rest. ` +
              `Particularly sensitive fields — such as names, birth dates, contact details, addresses, and dietary ` +
              `or medical notes — are additionally protected using database-level always-encrypted columns. Each ` +
              `group's data is isolated from every other group's, and access is restricted based on role.`,
          },
        ],
      },
      {
        id: 'retention',
        heading: 'Data retention',
        blocks: [
          {
            type: 'p',
            text:
              `We retain your personal information for as long as your account is active or as needed to provide ` +
              `the Service. When you delete your account or specific records, we remove them from our active ` +
              `systems promptly.`,
          },
          {
            type: 'p',
            text:
              `For integrity and recovery, our database keeps a temporal history of changes with a retention period ` +
              `of up to twelve months, and diagnostic logs are retained for approximately 90 days. Residual copies ` +
              `in this history and in encrypted backups are purged as they age out of these windows. Security audit ` +
              `records — which contain only identifiers, event types, and salted-hash network fingerprints, never ` +
              `names, notes, or other personal details — are retained longer for security and accountability.`,
          },
        ],
      },
      {
        id: 'rights',
        heading: 'Your choices and rights',
        blocks: [
          {
            type: 'p',
            text:
              `You can review and update your account details and much of your household data directly in the ` +
              `Service. You can delete your account at any time from your account settings; doing so erases your ` +
              `personal data as described in "Data retention." If you are the sole owner of a group that still has ` +
              `other members, you will first be asked to transfer ownership, remove the other members, or delete ` +
              `the group.`,
          },
          {
            type: 'p',
            text:
              `Although the Service is focused on users in the United States, we aim to honor the core ` +
              `data-protection rights recognized elsewhere — including the right to access, correct, or delete your ` +
              `personal information. To make such a request, contact us using the details below.`,
          },
        ],
      },
      {
        id: 'international',
        heading: 'International users',
        blocks: [
          {
            type: 'p',
            text:
              `The Service is operated from and hosted in the United States. If you access it from outside the ` +
              `United States, you understand that your information will be processed in the United States, where ` +
              `data-protection laws may differ from those in your location.`,
          },
        ],
      },
      {
        id: 'changes',
        heading: 'Changes to this Policy',
        blocks: [
          {
            type: 'p',
            text:
              `We may update this Privacy Policy from time to time. When we make material changes, we will update ` +
              `the "last updated" date above and, where appropriate, provide additional notice.`,
          },
        ],
      },
      {
        id: 'contact',
        heading: 'Contact',
        blocks: [{ type: 'contact' }],
      },
    ],
  }
}
