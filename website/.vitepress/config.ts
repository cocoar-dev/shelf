import { defineConfig } from 'vitepress'
import llmstxt from 'vitepress-plugin-llms'
import { execSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import fs from 'node:fs'

const dirname = path.dirname(fileURLToPath(import.meta.url))

// llms.txt / llms-full.txt are consumed out of band (agents fetching the URL directly,
// no repo checkout in hand) — a provenance line lets a consumer tell how stale their
// copy is. Degrades to 'unknown' when git isn't available in the build context.
function gitShortSha(): string {
  try {
    return execSync('git rev-parse --short HEAD', { cwd: dirname, stdio: ['ignore', 'pipe', 'ignore'] })
      .toString()
      .trim() || 'unknown'
  } catch {
    return 'unknown'
  }
}

const buildDate = new Date().toISOString().slice(0, 10)
// CI sets DOCS_VERSION (the docs slot like v2.0, from cd-deploy-docs input or the
// release version in cd-deploy-production); local builds omit the segment.
const docsVersion = process.env.DOCS_VERSION
const llmsTxtProvenance = `Generated: ${buildDate} · Source commit: ${gitShortSha()} · Product: Shelf · Canonical: https://docs.cocoar.dev/shelf/${docsVersion ? ` · Version: ${docsVersion}` : ''}`

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
      // The plugin's default llms.txt layout with one line of provenance metadata
      // after the description (same pattern as the modgud docs). `{description}`
      // already arrives with its own leading `> ` from the plugin. Only llms.txt
      // takes a template — llms-full.txt is a plain page concatenation.
      customLLMsTxtTemplate: `# {title}

{description}

> {provenance}

{details}

## Table of Contents

{toc}`,
      customTemplateVariables: {
        provenance: llmsTxtProvenance,
      },
    })],
  },

  // llms-full.txt is a plain concatenation with no template hook in this plugin
  // version — stamp the provenance header ourselves after the plugin has written
  // the file (same pattern as the modgud docs).
  async buildEnd(siteConfig) {
    const fullPath = path.join(siteConfig.outDir, 'llms-full.txt')
    if (!fs.existsSync(fullPath)) return
    const header = `# Shelf\n\n> ${llmsTxtProvenance}\n\n`
    fs.writeFileSync(fullPath, header + fs.readFileSync(fullPath, 'utf-8'))
  },

  themeConfig: {
    logo: {
      light: '/logo_light.svg',
      dark: '/logo_dark.svg',
    },

    siteTitle: 'Shelf',

    nav: [
      { text: 'Guide', link: '/guide/getting-started' },
      // llms.txt (the summary), NOT llms-full.txt — modgud convention. Note: VitePress
      // skips withBase for .txt targets at hydration, so under /shelf/<version>/ the
      // click lands on the ROOT /llms.txt — which is Shelf's product index (useful);
      // a root /llms-full.txt would 404, which is why that variant must not come back.
      { text: 'LLM Docs', link: '/llms.txt', target: '_blank' },
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
