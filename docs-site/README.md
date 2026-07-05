# Gatherstead Docs Site

The public documentation site for Gatherstead, built with [Astro](https://astro.build/) +
[Starlight](https://starlight.astro.build/) and published to GitHub Pages at
**[docs.gatherstead.app](https://docs.gatherstead.app)**.

The user-facing guide lives here (as the canonical source); the engineering docs under the
repo's `docs/` folder stay repo-internal.

## Local development

```sh
pnpm install
pnpm dev        # http://localhost:4321
pnpm build      # output to dist/
pnpm preview    # serve the built site locally
```

> The rest of the monorepo uses pnpm; this site is a standalone pnpm project (its own
> `package.json`) and is intentionally excluded from the .NET/Nuxt build gates.

## Content

- Pages are MDX under `src/content/docs/`. The guide lives in `src/content/docs/guide/`.
- Sidebar order comes from each page's `sidebar.order` frontmatter; the sidebar groups are
  configured in `astro.config.mjs`.

### Role-tagged sections and filtering

Each section is tagged with the lowest role that can use it, via the `<AccessBadge>` component
placed directly under the heading:

```mdx
## Setting up properties

<AccessBadge level="manager" />
```

Valid `level` tokens (the stable vocabulary): `everyone | member | coordinator | manager | owner`.

The **role filter** lives once in the left navigation, injected via a Starlight
[component override](https://starlight.astro.build/guides/overriding-components/): `astro.config.mjs`
maps `components.Sidebar` → `src/components/DocsSidebar.astro`, which renders `<RoleFilter />`
above the default sidebar. It reads each section's `data-access` token and hides sections above the
reader's chosen role — in the page body **and** in the on-this-page table of contents — persisting
the choice in `localStorage`. To make a section filterable, give it a heading **immediately
followed** by an `<AccessBadge>`. (Pages no longer include `<RoleFilter />` themselves.)

## Deployment

`.github/workflows/docs.yml` builds this folder and deploys it to GitHub Pages on any push to
`main` that touches `docs-site/**`. It can also be run manually (workflow_dispatch).

### One-time setup

1. **Enable Pages:** repo **Settings → Pages → Build and deployment → Source: GitHub Actions**.
2. **Custom domain (DNS):** add a `CNAME` record `docs` → `jayceslayn.github.io` at the DNS
   provider for `gatherstead.app`. The `public/CNAME` file pins the domain on each deploy.
3. **HTTPS:** once DNS resolves, tick **Enforce HTTPS** in Settings → Pages.
4. **Commit** `pnpm-lock.yaml` and `pnpm-workspace.yaml` — CI installs with `--frozen-lockfile`
   and honours the `allowBuilds` allowlist (esbuild, sharp) for native build scripts.
