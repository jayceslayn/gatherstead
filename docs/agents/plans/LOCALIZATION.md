# Localization (i18n)

This document describes the internationalization strategy for the Gatherstead web app. The goal is to establish i18n patterns from day one so all future UI development follows a consistent, localizable approach.

## Module Setup

Use `@nuxtjs/i18n` (the official Nuxt module wrapping `vue-i18n`).

**Install:**
```bash
pnpm add @nuxtjs/i18n
```

**Configuration in [`nuxt.config.ts`](../src/Gatherstead.Web/nuxt.config.ts):**

Add `'@nuxtjs/i18n'` to the `modules` array and add the `i18n` config block:

```ts
i18n: {
  defaultLocale: 'en',
  langDir: 'locales',
  lazy: true,
  locales: [
    { code: 'en', language: 'en-US', file: 'en.json', name: 'English' },
    // Add new locales here, e.g.:
    // { code: 'es', language: 'es-MX', file: 'es.json', name: 'Espanol' },
  ],
  strategy: 'prefix_except_default',
  detectBrowserLanguage: {
    useCookie: true,
    cookieKey: 'i18n_locale',
    fallbackLocale: 'en',
    redirectOn: 'root',
  },
  numberFormats: {
    en: {
      currency: { style: 'currency', currency: 'USD' },
      decimal: { style: 'decimal', minimumFractionDigits: 2 },
    },
  },
  datetimeFormats: {
    en: {
      short: { year: 'numeric', month: 'short', day: 'numeric' },
      long: { year: 'numeric', month: 'long', day: 'numeric', weekday: 'long' },
      time: { hour: 'numeric', minute: 'numeric' },
    },
  },
}
```

### Key Configuration Decisions

- **`prefix_except_default`** strategy: English URLs stay clean (`/dashboard`), other locales get a prefix (`/es/dashboard`). This is the SEO-recommended approach -- search engines see distinct URLs per language, and the module auto-generates `hreflang` link tags and sets `<html lang="">`.
- **Lazy loading**: Locale files are loaded on demand, so adding many languages does not bloat the initial bundle.
- **Cookie persistence**: Remembers the user's language choice across sessions without depending on authentication state.
- **Browser detection**: Only redirects on the root path (`/`) to avoid disrupting deep links.

## Locale File Structure

Create `app/locales/en.json` with nested keys organized by domain context:

```json
{
  "common": {
    "save": "Save",
    "cancel": "Cancel",
    "delete": "Delete",
    "edit": "Edit",
    "create": "Create",
    "loading": "Loading...",
    "error": "An error occurred",
    "confirm": "Are you sure?",
    "search": "Search",
    "noResults": "No results found"
  },
  "nav": {
    "dashboard": "Dashboard",
    "households": "Households",
    "events": "Events",
    "properties": "Properties",
    "settings": "Settings"
  },
  "household": {
    "title": "Households",
    "createTitle": "Create Household",
    "editTitle": "Edit Household",
    "name": "Household Name",
    "memberCount": "{count} member | {count} members",
    "searchPlaceholder": "Search households...",
    "roles": {
      "admin": "Admin",
      "member": "Member"
    }
  },
  "member": {
    "title": "Members",
    "name": "Name",
    "birthDate": "Birth Date",
    "dietaryNotes": "Dietary Notes",
    "isAdult": "Adult",
    "ageBand": "Age Band"
  },
  "event": {
    "title": "Events",
    "createTitle": "Create Event",
    "name": "Event Name",
    "startDate": "Start Date",
    "endDate": "End Date",
    "mealPlan": "Meal Plan",
    "choreTemplate": "Chore Template",
    "attendance": "Attendance"
  },
  "property": {
    "title": "Properties",
    "name": "Property Name",
    "resources": "Resources"
  },
  "validation": {
    "required": "{field} is required",
    "maxLength": "{field} must be {max} characters or fewer",
    "invalidEmail": "Please enter a valid email address",
    "invalidDate": "Please enter a valid date"
  },
  "error": {
    "notFound": "The requested resource was not found",
    "forbidden": "You do not have permission to perform this action",
    "serverError": "Something went wrong. Please try again later.",
    "networkError": "Unable to connect. Please check your internet connection."
  },
  "apiError": {},
  "demo": {}
}
```

### Key Naming Conventions

- Top-level keys correspond to domain contexts or cross-cutting concerns
- Use camelCase for key names
- Group related strings under their domain context (e.g., `household.createTitle`, not `createHouseholdTitle`)
- Keep `common.*` for strings reused across multiple contexts
- `validation.*` uses `{field}` interpolation so field names can be localized independently
- `apiError.*` keys map directly from API error codes (see API Error Translation below)
- `demo.*` keys are for demo mode UI (see [DEMO_SITE.md](DEMO_SITE.md))

## Usage Conventions

These conventions apply to **all future UI development**. Every piece of user-visible text must go through the i18n system.

### In Templates

The `$t()` function is auto-imported and available in all templates:

```html
<h1>{{ $t('household.title') }}</h1>
<UButton :label="$t('common.save')" />
<UInput :placeholder="$t('household.searchPlaceholder')" />
```

### In Script Setup

Use the `useI18n()` composable:

```ts
const { t, n, d } = useI18n()
const title = computed(() => t('household.title'))
```

### Interpolation

Use named parameters for dynamic values:

```html
{{ $t('validation.required', { field: $t('household.name') }) }}
```

### Pluralization

Use the pipe syntax in locale files:

```json
{ "memberCount": "{count} member | {count} members" }
```

```html
{{ $t('household.memberCount', { count: members.length }) }}
```

### Date and Number Formatting

Use `$d()` and `$n()` which automatically adapt to the current locale:

```html
<span>{{ $d(event.startDate, 'short') }}</span>
<span>{{ $n(amount, 'currency') }}</span>
```

### Nuxt UI Components

Nuxt UI components accept label/placeholder/aria props as strings. Always pass translated values:

```html
<UButton :label="$t('common.save')" />
<UModal :title="$t('household.createTitle')">
<UInput :placeholder="$t('member.name')" />
```

## API Error Translation

The API returns structured error responses with error codes (not human-readable messages). The client maps these codes to locale keys.

### Strategy

1. **API returns error codes**: e.g., `{ "code": "HOUSEHOLD_LIMIT_REACHED", "detail": "Maximum households per tenant: 50" }`
2. **Client maps codes to locale keys**: `apiError.HOUSEHOLD_LIMIT_REACHED`
3. **Fallback**: If no matching locale key exists, display the raw `detail` from the API (English). This ensures new error codes degrade gracefully before translations catch up.

### Implementation

Create `app/composables/useApiError.ts`:

```ts
export function useApiError() {
  const { t, te } = useI18n()

  function translateError(apiError: { code: string; detail?: string }): string {
    const key = `apiError.${apiError.code}`
    return te(key) ? t(key) : apiError.detail ?? t('error.serverError')
  }

  return { translateError }
}
```

Usage in components:

```ts
const { translateError } = useApiError()
const toast = useToast()

try {
  await householdService.create(data)
} catch (error) {
  toast.add({ title: translateError(error), color: 'error' })
}
```

## Locale Switcher Component

Create `app/components/LocaleSwitcher.vue` using Nuxt UI's `UDropdownMenu`:

- Lists all configured locales from `useI18n().availableLocales`
- Displays locale names (e.g., "English", "Espanol")
- Calls `setLocale(code)` on selection
- The `@nuxtjs/i18n` module handles cookie persistence and route prefix updates automatically
- Include in the app's navigation/header layout

## SEO

With the `prefix_except_default` strategy, `@nuxtjs/i18n` automatically handles:

- `<html lang="en">` attribute (updates per locale)
- `<link rel="alternate" hreflang="es" href="/es/...">` tags for each configured locale
- `og:locale` meta tags
- Distinct URLs per language for search engine indexing

No additional manual SEO work is needed beyond configuring locales.

## Linting

Add the `@intlify/vue-i18n/no-raw-text` ESLint rule to catch hardcoded strings in templates. This is critical for enforcement as the codebase grows and multiple developers contribute.

**Install:**
```bash
pnpm add -D @intlify/eslint-plugin-vue-i18n
```

Configure the rule to flag any raw text in Vue templates that should be wrapped in `$t()`.

## Testing

- **Unit tests**: Verify that `useApiError` correctly maps error codes to locale keys and falls back to raw detail text when no key exists.
- **Component tests**: Use `@vue/test-utils` with a mock i18n plugin to verify components render translated text. Inject a test i18n instance via the `global.plugins` option.
- **Locale completeness**: When adding a new locale file, ensure all keys from `en.json` are present. Consider a CI check that compares key sets across locale files.
- **ESLint enforcement**: The `no-raw-text` rule catches hardcoded strings at lint time, preventing regressions.

## Files Summary

| File | Action |
|------|--------|
| `src/Gatherstead.Web/package.json` | Add `@nuxtjs/i18n` and `@intlify/eslint-plugin-vue-i18n` |
| `src/Gatherstead.Web/nuxt.config.ts` | Add module + i18n config block |
| `src/Gatherstead.Web/app/locales/en.json` | **Create** -- English locale messages |
| `src/Gatherstead.Web/app/composables/useApiError.ts` | **Create** -- API error code translation |
| `src/Gatherstead.Web/app/components/LocaleSwitcher.vue` | **Create** -- Locale switcher dropdown |

## Adding a New Language

1. Create a new locale file (e.g., `app/locales/es.json`) with all keys from `en.json` translated
2. Add the locale entry to `nuxt.config.ts`:
   ```ts
   { code: 'es', language: 'es-MX', file: 'es.json', name: 'Espanol' }
   ```
3. Add number/date formats for the new locale in the i18n config
4. The locale switcher and URL routing update automatically
