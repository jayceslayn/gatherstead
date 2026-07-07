/// <reference types="astro/client" />
interface ImportMetaEnv {
  readonly PUBLIC_DEMO_URL?: string;
  readonly PUBLIC_LIVE_URL?: string;
}
interface ImportMeta {
  readonly env: ImportMetaEnv;
}
