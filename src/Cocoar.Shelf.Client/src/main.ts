import { createApp } from 'vue';
import { createPinia } from 'pinia';
import { CoarIconPlugin, CoarOverlayPlugin, CoarHttpIconSource, CORE_ICONS } from '@cocoar/vue-ui';
import App from './App.vue';
import { router } from './router';
import '@cocoar/vue-ui/styles';
import './styles.css';

const app = createApp(App);
app.use(createPinia());
app.use(router);
app.use(CoarIconPlugin, {
  sources: [
    CORE_ICONS,
    {
      key: 'lucide',
      source: new CoarHttpIconSource((name) => `/icons/lucide/${name}.svg`),
    },
  ],
  defaultSource: 'lucide',
});
app.use(CoarOverlayPlugin);
app.mount('#app');
