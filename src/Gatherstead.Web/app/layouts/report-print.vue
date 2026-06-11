<script setup lang="ts">
// Chrome-free layout for printable documents. No sidebar, nav, breadcrumbs, or banners —
// just the document, so a browser print produces a clean sheet. On screen it renders as a
// centred document preview.
</script>

<template>
  <div class="report-print min-h-screen bg-(--ui-bg-muted) print:bg-white">
    <div class="mx-auto max-w-3xl bg-(--ui-bg) text-default p-6 sm:p-10 print:p-0 print:max-w-none">
      <slot />
    </div>
  </div>
</template>

<style>
@page {
  margin: 16mm 14mm;
}

@media print {
  /* Let the document own the full sheet; strip any app background. */
  html,
  body {
    background: #fff !important;
  }
}
</style>
