// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

// Served at the apex of the docs subdomain, so `base` stays '/' (do not set it to the repo name).
// The custom domain is pinned by public/CNAME → docs.gatherstead.app.
export default defineConfig({
  site: 'https://docs.gatherstead.app',
  integrations: [
    starlight({
      title: 'Gatherstead',
      description:
        'How to use Gatherstead — onboarding, households, properties, events, and shopping, tagged by the role that can use each step.',
      logo: {
        // The colored house-mark reads on both light and dark chrome; the theme-aware
        // title text supplies the wordmark. (The full wordmark PNG has dark text that
        // would be illegible on the dark sidebar, so it isn't used as the logo.)
        src: './src/assets/gatherstead-mark.png',
        alt: 'Gatherstead',
      },
      favicon: '/favicon.png',
      customCss: ['./src/styles/custom.css'],
      components: {
        // Move the role filter into the global left nav so it isn't repeated on every page.
        Sidebar: './src/components/DocsSidebar.astro',
      },
      social: [
        { icon: 'github', label: 'GitHub', href: 'https://github.com/jayceslayn/gatherstead' },
      ],
      editLink: {
        baseUrl: 'https://github.com/jayceslayn/gatherstead/edit/main/docs-site/',
      },
      sidebar: [
        {
          label: 'User Guide',
          items: [
            { label: 'Roles & access', slug: 'guide/roles-and-access' },
            { label: 'Getting started', slug: 'guide/getting-started' },
            { label: 'Directory: households & members', slug: 'guide/directory' },
            { label: 'Places & things', slug: 'guide/places-and-things' },
            { label: 'Events', slug: 'guide/events' },
            { label: 'Shopping list', slug: 'guide/shopping-list' },
            { label: 'Everyday extras', slug: 'guide/everyday-extras' },
          ],
        },
      ],
    }),
  ],
});
