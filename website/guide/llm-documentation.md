# LLM Documentation

When AI assistants (Claude, ChatGPT, etc.) fetch a documentation page, they receive the raw HTML but typically don't execute JavaScript. Single-page applications and JavaScript-heavy frameworks render very little useful content without JS. This means an AI fetching your docs may get almost nothing.

The solution: provide plain-text versions of your documentation that are optimized for LLM consumption.

## The Convention

The emerging standard (see [llmstxt.org](https://llmstxt.org/)) is to serve two plain-text files alongside your documentation:

| File | Purpose |
|------|---------|
| `llms.txt` | Short summary — product description, links, and table of contents |
| `llms-full.txt` | Complete documentation as plain Markdown |

These files should be served at the root of your documentation:

```
https://docs.example.com/myproduct/v5/llms.txt
https://docs.example.com/myproduct/v5/llms-full.txt
```

Shelf serves these files like any other static file — just include them in your deployment archive.

## Making the Files Discoverable

### Link Tags in the HTML Head

Add `<link>` tags to your HTML `<head>` so AI tools can find the LLM-optimized versions when they fetch any page:

```html
<link rel="alternate" type="text/plain" href="/llms.txt"
      title="LLM documentation (summary)">
<link rel="alternate" type="text/plain" href="/llms-full.txt"
      title="LLM documentation (full)">
```

The leading `/` makes the path root-relative. Shelf's [base path rewriting](./base-path-rewriting.md) rewrites it to include the correct product/version prefix (e.g., `/configuration/v5/llms.txt`).

This is similar to how RSS feeds are discoverable via `<link rel="alternate" type="application/rss+xml">`.

### Visible Link

Additionally, add a visible link in your navigation or footer. This helps users who want to share documentation with AI assistants, and some AI tools also follow visible links. For example, the [Marten documentation](https://martendb.io/) includes a "LLM Friendly Docs" link in the top navigation.

### Using Both

We recommend using both approaches. The `<link>` tags are what AI tools parse programmatically, while the visible link helps human users discover the feature.

## VitePress Setup

For VitePress projects, the [`vitepress-plugin-llms`](https://github.com/okineadev/vitepress-plugin-llms) plugin handles everything automatically. It's used by major projects including Vite, Vue.js, and Vitest.

### Installation

```bash
npm install -D vitepress-plugin-llms
```

### Configuration

```ts
// .vitepress/config.ts
import { defineConfig } from 'vitepress'
import llmstxt from 'vitepress-plugin-llms'

export default defineConfig({
  // Link tags for discoverability
  head: [
    ['link', {
      rel: 'alternate',
      type: 'text/plain',
      href: '/llms.txt',
      title: 'LLM documentation (summary)'
    }],
    ['link', {
      rel: 'alternate',
      type: 'text/plain',
      href: '/llms-full.txt',
      title: 'LLM documentation (full)'
    }],
  ],

  // Plugin generates llms.txt and llms-full.txt during build
  vite: {
    plugins: [llmstxt()],
  },

  themeConfig: {
    nav: [
      // Visible link in the navigation bar
      { text: 'LLM Docs', link: '/llms-full.txt', target: '_blank' },
    ],
  },
})
```

That's it. When you run `vitepress build`, the plugin:

1. Reads all Markdown pages from the sidebar
2. Generates `llms.txt` (summary with links) and `llms-full.txt` (complete documentation)
3. Includes them in the build output

The generated files end up in your deployment archive automatically.

### Shelf Integration

Shelf's [base path rewriting](./base-path-rewriting.md) takes care of rewriting the `href` values in the `<link>` tags to include the correct product/version prefix. No additional configuration needed.

## Other Frameworks

For non-VitePress documentation, the approach is the same:

1. Generate `llms.txt` and `llms-full.txt` as part of your build process
2. Include them in the deployment archive alongside your HTML
3. Add `<link>` tags and a visible link for discoverability

How you generate the files depends on your framework and tooling. The content should be clean, readable Markdown without framework-specific syntax.
