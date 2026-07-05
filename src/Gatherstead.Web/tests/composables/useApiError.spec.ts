import { describe, expect, it, vi } from 'vitest'

// translateError depends only on the Nuxt-auto-imported useI18n(). Stub it with a tiny i18n that
// knows a couple of apiError templates + the entity token, mirroring the real locale entries, and
// does {name}/{entity} named interpolation the way vue-i18n does.
const messages: Record<string, string> = {
  'error.serverError': 'Something went wrong. Please try again later.',
  'apiError.entity.accommodation': 'accommodation',
  'apiError.ENTITY_CONFLICT': "An {entity} named '{name}' already exists here.",
}

function interpolate(template: string, params: Record<string, string>): string {
  return template.replace(/\{(\w+)\}/g, (_, k: string) => params[k] ?? `{${k}}`)
}

const t = (key: string, params?: Record<string, string>) =>
  key in messages ? interpolate(messages[key]!, params ?? {}) : key
const te = (key: string) => key in messages

vi.stubGlobal('useI18n', () => ({ t, te }))

const { useApiError } = await import('../../app/composables/useApiError')
const { translateError } = useApiError()

describe('translateError', () => {
  it('renders a localized template from the error code, resolving the entity token and interpolating params', () => {
    const e = { data: { messages: [{ type: 'ERROR', code: 'ENTITY_CONFLICT', message: 'raw english', params: { entity: 'accommodation', name: 'Lakeside Cabin' } }] } }
    expect(translateError(e)).toBe("An accommodation named 'Lakeside Cabin' already exists here.")
  })

  it('falls back to the entity token literally when no i18n entry exists for it', () => {
    const e = { data: { messages: [{ type: 'ERROR', code: 'ENTITY_CONFLICT', message: 'raw', params: { entity: 'widget', name: 'W' } }] } }
    expect(translateError(e)).toBe("An widget named 'W' already exists here.")
  })

  it('falls back to the server message when the code has no template', () => {
    const e = { data: { messages: [{ type: 'ERROR', code: 'SOME_UNMAPPED_CODE', message: 'A descriptive server message.' }] } }
    expect(translateError(e)).toBe('A descriptive server message.')
  })

  it('uses the server message when there is no code at all (legacy envelope)', () => {
    const e = { data: { messages: [{ type: 'ERROR', message: 'Legacy message.' }] } }
    expect(translateError(e)).toBe('Legacy message.')
  })

  it('flattens ASP.NET ValidationProblemDetails errors', () => {
    const e = { data: { errors: { Name: ['The Name field is required.'], Type: ['Invalid.'] } } }
    expect(translateError(e)).toBe('The Name field is required. Invalid.')
  })

  it('returns ProblemDetails detail when present', () => {
    const e = { data: { detail: 'Something specific went wrong.' } }
    expect(translateError(e)).toBe('Something specific went wrong.')
  })

  it('never surfaces the raw ofetch route/status string — falls back to serverError', () => {
    const e = { message: '[POST] "/api/proxy/.../accommodations": 400 Bad Request' }
    expect(translateError(e)).toBe(messages['error.serverError'])
  })

  it('picks the first ERROR message, ignoring warnings', () => {
    const e = { data: { messages: [
      { type: 'WARNING', message: 'just a warning' },
      { type: 'ERROR', message: 'the real error' },
    ] } }
    expect(translateError(e)).toBe('the real error')
  })
})
