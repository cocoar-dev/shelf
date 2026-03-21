declare module '@cocoar/vue-ui' {
  import type { Plugin, Component } from 'vue';

  export const CoarIconPlugin: Plugin;
  export const CoarOverlayPlugin: Plugin;
  export const CORE_ICONS: unknown;

  export class CoarHttpIconSource {
    constructor(resolver: (name: string) => string);
  }

  export const CoarButton: Component;
  export const CoarCard: Component;
  export const CoarCheckbox: Component;
  export const CoarIcon: Component;
  export const CoarNote: Component;
  export const CoarOverlayHost: Component;
  export const CoarTextInput: Component;
}

declare module '@cocoar/vue-ui/styles' {}
