// @ts-check
import vueI18n from '@intlify/eslint-plugin-vue-i18n'
import withNuxt from './.nuxt/eslint.config.mjs'

export default withNuxt(
  { ignores: ['app/repositories/generated/**'] },
  ...vueI18n.configs['flat/recommended'],
  {
    rules: {
      '@intlify/vue-i18n/no-raw-text': 'error',
    },
    settings: {
      'vue-i18n': {
        localeDir: './app/locales/*.json',
      },
    },
  },
)
