// @ts-check
import vueI18n from '@intlify/eslint-plugin-vue-i18n'

export default [
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
]
