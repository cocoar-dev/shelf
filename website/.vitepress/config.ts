import { defineConfig } from 'vitepress'
import llmstxt from 'vitepress-plugin-llms'

export default defineConfig({
  title: 'Shelf',
  description: 'Static documentation hosting for Cocoar products',

  head: [
    ['link', { rel: 'icon', type: 'image/svg+xml', href: '/logo_light.svg' }],
    ['link', { rel: 'alternate', type: 'text/plain', href: '/llms.txt', title: 'LLM documentation (summary)' }],
    ['link', { rel: 'alternate', type: 'text/plain', href: '/llms-full.txt', title: 'LLM documentation (full)' }],
  ],

  vite: {
    plugins: [llmstxt({
      excludeUnnecessaryFiles: false,
      ignoreFiles: ['changelog.md'],
    })],
  },

  themeConfig: {
    logo: {
      light: '/logo_light.svg',
      dark: '/logo_dark.svg',
    },

    siteTitle: 'Shelf',

    nav: [
      { text: 'Guide', link: '/guide/getting-started' },
      { text: 'LLM Docs', link: '/llms-full.txt', target: '_blank' },
    ],

    sidebar: {
      '/guide/': [
        {
          text: 'Introduction',
          items: [
            { text: 'Getting Started', link: '/guide/getting-started' },
            { text: 'How It Works', link: '/guide/how-it-works' },
          ],
        },
        {
          text: 'Setup',
          items: [
            { text: 'Docker', link: '/guide/docker' },
            { text: 'Configuration', link: '/guide/configuration' },
            { text: 'Authentication', link: '/guide/authentication' },
          ],
        },
        {
          text: 'Deployment',
          items: [
            { text: 'Product Registration', link: '/guide/product-registration' },
            { text: 'Upload API', link: '/guide/upload-api' },
            { text: 'Admin UI', link: '/guide/admin-ui' },
          ],
        },
        {
          text: 'Details',
          items: [
            { text: 'URL Routing', link: '/guide/url-routing' },
            { text: 'Base Path Rewriting', link: '/guide/base-path-rewriting' },
            { text: 'Caching', link: '/guide/caching' },
            { text: 'LLM Documentation', link: '/guide/llm-documentation' },
            { text: 'Troubleshooting', link: '/guide/troubleshooting' },
          ],
        },
      ],
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/cocoar-dev/shelf' },
    ],

    search: {
      provider: 'local',
    },

    footer: {
      message: 'Shelf your Docs.',
      copyright: 'Copyright 2025-present Cocoar',
    },
  },
})
