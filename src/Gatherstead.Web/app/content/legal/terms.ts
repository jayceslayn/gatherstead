import type { LegalContext, LegalDoc } from './types'

/**
 * Terms of Service — English-only authoritative template (see the note in `types.ts`).
 * This is a tailored starting point, not legal advice; have it reviewed by counsel before relying
 * on it. Deployment-specific identity comes from `ctx` (runtime config), never hardcoded.
 */
export function buildTermsDoc(ctx: LegalContext): LegalDoc {
  const { providerName, jurisdiction, contactEmail, appName, effectiveDate } = ctx
  const governingState = jurisdiction ? `the State of ${jurisdiction}` : 'the state in which the Provider is established'

  return {
    title: 'Terms of Service',
    effectiveDate,
    contactEmail,
    intro:
      `These Terms of Service ("Terms") govern your access to and use of ${appName} (the "Service"), ` +
      `operated by ${providerName} ("we," "us," or the "Provider"). By accessing or using the Service, ` +
      `you agree to be bound by these Terms. If you do not agree, do not use the Service.`,
    sections: [
      {
        id: 'eligibility',
        heading: 'Eligibility and access',
        blocks: [
          {
            type: 'p',
            text:
              `The Service is currently offered on an invitation-only basis. You may access it only if you ` +
              `have received a valid invitation or been granted access by an existing group, and only for the ` +
              `purposes described in these Terms.`,
          },
          {
            type: 'p',
            text:
              `You must be at least 18 years old to create an account or agree to these Terms. The Service is ` +
              `intended for adults who organize gatherings on behalf of their households, families, or ` +
              `organizations. It is not directed to children, and children may not create their own accounts.`,
          },
        ],
      },
      {
        id: 'accounts',
        heading: 'Accounts and authentication',
        blocks: [
          {
            type: 'p',
            text:
              `Sign-in and account credentials are handled by our third-party identity provider (Microsoft ` +
              `Entra External ID). We do not store your password. You are responsible for keeping your ` +
              `sign-in credentials confidential and for all activity that occurs under your account.`,
          },
          {
            type: 'p',
            text:
              `You agree to provide accurate information and keep it up to date, and you are responsible for ` +
              `the information you add to the Service — including information about other people, as described ` +
              `below.`,
          },
        ],
      },
      {
        id: 'acceptable-use',
        heading: 'Acceptable use',
        blocks: [
          {
            type: 'p',
            text: `You agree to use the Service only for lawful purposes and in accordance with these Terms. You agree not to:`,
          },
          {
            type: 'list',
            items: [
              `Use the Service in violation of any applicable law or regulation;`,
              `Upload or enter information about another person without the authority or consent to do so;`,
              `Attempt to gain unauthorized access to the Service, other accounts, or any connected systems or networks;`,
              `Interfere with, disrupt, or place an unreasonable load on the Service or its infrastructure;`,
              `Use the Service to store or transmit malicious code, or to harass, abuse, or harm another person;`,
              `Scrape or resell the Service, or systematically extract data from it, except to the extent permitted by law.`,
            ],
          },
          {
            type: 'p',
            text:
              `These Terms govern the hosted Service only. The Service's source code is published separately ` +
              `under an open-source license (GNU AGPL-3.0), and that license — not these Terms — governs your ` +
              `use, study, and modification of the code itself.`,
          },
        ],
      },
      {
        id: 'your-content',
        heading: 'Your content and information about others',
        blocks: [
          {
            type: 'p',
            text:
              `The Service lets you record information about your household and its members — which may include ` +
              `names, birth dates, contact details, addresses, dietary or medical notes, and relationships. Much ` +
              `of this information concerns people other than you, and may include minors.`,
          },
          {
            type: 'p',
            text:
              `You represent and warrant that you have the authority and, where required, the consent to provide ` +
              `this information and to have it processed through the Service. You are solely responsible for the ` +
              `accuracy of the information you enter and for ensuring your use respects the rights and expectations ` +
              `of the people it concerns.`,
          },
          {
            type: 'p',
            text:
              `You retain ownership of the information and content you provide. You grant us a limited, ` +
              `non-exclusive license to host, store, process, and display that content solely to operate and ` +
              `provide the Service to you and the groups you belong to. We do not sell your content or use it for ` +
              `advertising. See our Privacy Policy for details.`,
          },
        ],
      },
      {
        id: 'fees',
        heading: 'Fees and future paid features',
        blocks: [
          {
            type: 'p',
            text:
              `The Service is currently provided free of charge on an invitation-only basis. We may introduce ` +
              `paid plans or subscription features in the future. If we do, we will give you notice and an ` +
              `opportunity to review the applicable pricing and terms before any charges apply. We will not begin ` +
              `charging you for features you are already using without your consent.`,
          },
        ],
      },
      {
        id: 'no-medical',
        heading: 'Not a medical or emergency service',
        blocks: [
          {
            type: 'p',
            text:
              `Any dietary, allergy, medical, or similar notes stored in the Service are provided by users for ` +
              `convenience and coordination only. The Service is not a medical record system and must not be relied ` +
              `upon for medical, dietary, or emergency decisions. Always rely on qualified professionals and ` +
              `primary sources for health and safety information.`,
          },
        ],
      },
      {
        id: 'availability',
        heading: 'Availability and changes to the Service',
        blocks: [
          {
            type: 'p',
            text:
              `We may modify, suspend, or discontinue any part of the Service at any time, with or without notice. ` +
              `We are not liable for any modification, suspension, or discontinuation of the Service or any part of it.`,
          },
        ],
      },
      {
        id: 'warranty',
        heading: 'Disclaimer of warranties',
        blocks: [
          {
            type: 'p',
            text:
              `The Service is provided "as is" and "as available," without warranties of any kind, whether express ` +
              `or implied, including implied warranties of merchantability, fitness for a particular purpose, and ` +
              `non-infringement. We do not warrant that the Service will be uninterrupted, secure, error-free, or ` +
              `free from data loss.`,
          },
        ],
      },
      {
        id: 'liability',
        heading: 'Limitation of liability',
        blocks: [
          {
            type: 'p',
            text:
              `To the maximum extent permitted by law, the Provider will not be liable for any indirect, incidental, ` +
              `special, consequential, or punitive damages, or for any loss of data, profits, or goodwill, arising ` +
              `out of or related to your use of the Service. To the maximum extent permitted by law, our total ` +
              `liability for any claim arising out of or related to the Service will not exceed the greater of the ` +
              `amount you paid us for the Service in the twelve months before the claim, or US $100.`,
          },
        ],
      },
      {
        id: 'indemnity',
        heading: 'Indemnification',
        blocks: [
          {
            type: 'p',
            text:
              `You agree to indemnify and hold harmless the Provider from any claims, damages, liabilities, and ` +
              `expenses (including reasonable legal fees) arising out of your use of the Service, your content, your ` +
              `violation of these Terms, or your violation of the rights of any other person — including information ` +
              `you enter about others.`,
          },
        ],
      },
      {
        id: 'termination',
        heading: 'Termination and account deletion',
        blocks: [
          {
            type: 'p',
            text:
              `You may stop using the Service at any time and may delete your account from your account settings. ` +
              `Deleting your account removes your personal data as described in our Privacy Policy. If you are the ` +
              `sole owner of a group that still has other members, you will first be asked to transfer ownership, ` +
              `remove the other members, or delete the group. We may suspend or terminate your access if you ` +
              `violate these Terms or if we discontinue the Service.`,
          },
        ],
      },
      {
        id: 'governing-law',
        heading: 'Governing law and disputes',
        blocks: [
          {
            type: 'p',
            text:
              `These Terms are governed by the laws of ${governingState}, without regard to its conflict-of-laws principles. ` +
              `Any dispute arising out of or relating to these Terms or the Service will be subject to the exclusive ` +
              `jurisdiction of the state and federal courts located in ${governingState}, and you consent to personal ` +
              `jurisdiction there.`,
          },
        ],
      },
      {
        id: 'changes',
        heading: 'Changes to these Terms',
        blocks: [
          {
            type: 'p',
            text:
              `We may update these Terms from time to time. When we make material changes, we will update the ` +
              `"last updated" date above and, where appropriate, provide additional notice. Your continued use of ` +
              `the Service after changes take effect constitutes acceptance of the revised Terms.`,
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
