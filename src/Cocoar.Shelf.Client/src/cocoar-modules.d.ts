declare module '@cocoar/vue-ui' {
  import type { Plugin, Component } from 'vue';

  export const CoarOverlayPlugin: Plugin;
  export const CoarOverlayHost: Component;
  export const CoarIconPlugin: Plugin;
  export class CoarIconMapSource {
    constructor(icons: Record<string, string>);
  }
  export class CoarHttpIconSource {
    constructor(resolver: (name: string) => string);
  }
  export const CORE_ICONS: unknown;

  export const CoarButton: Component;
  export const CoarCard: Component;
  export const CoarCheckbox: Component;
  export const CoarIcon: Component;
  export const CoarNote: Component;
  export const CoarSelect: Component;
  export const CoarSpinner: Component;
  export const CoarTable: Component;
  export const CoarTag: Component;
  export const CoarTextInput: Component;

  export const CoarSidebar: Component;
  export const CoarSidebarItem: Component;
  export const CoarSidebarGroup: Component;
  export const CoarSidebarHeading: Component;
  export const CoarSidebarDivider: Component;
  export const CoarSidebarSpacer: Component;

  export const CoarContextMenu: Component;
  export const CoarMenuItem: Component;
  export const CoarMenuDivider: Component;
  export const CoarMenu: Component;

  export function useContextMenu(): { open: (event: MouseEvent | { clientX: number; clientY: number }) => void; close: () => void };
}

declare module '@cocoar/vue-ui/styles' {}
declare module '@cocoar/vue-ui/fonts' {}

declare module '@cocoar/vue-data-grid' {
  import type { Component, Ref } from 'vue';
  export const CoarDataGrid: Component;
  export class CoarGridBuilder<T> {
    static create<T>(): CoarGridBuilder<T>;
    persistColumnState(key: string): this;
    option(key: string, value: any): this;
    rowDataRef(ref: Ref<T[]> | any): this;
    searchHighlight(): this;
    rowSelection(mode: 'single' | 'multiple'): this;
    onCellDoubleClicked(handler: (event: any) => void): this;
    onCellContextMenu(handler: (event: any) => void): this;
    onViewportContextMenu(handler: (event: any) => void): this;
    columns(cols: Array<(col: any) => any>): this;
  }
  export function cleanupColumnStates(maxDays: number): void;
}

declare module '@cocoar/vue-data-grid/styles' {}

declare module '@cocoar/vue-fragment-parser' {
  import type { Component } from 'vue';
  export function useRoutedModals(): void;
  export function useFragmentNavigation(): { navigateToModal: (id: string) => void; closeModal: () => void };
}

declare interface Window {
  __SHELF_OPTIONS__: { pathBase: string };
}
