import { createRouter, createWebHistory } from 'vue-router'
import YubiKeyVerify from './components/YubiKeyVerify.vue'
import InvalidVerification from './components/InvalidVerification.vue'

// GUID validation function
const isValidFormat = (jti: string): boolean => {
  const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  return guidRegex.test(jti);
};

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/verify/:jti',
      name: 'verify',
      component: YubiKeyVerify,
      beforeEnter: (to) => {
        const jti = to.params.jti as string;
        if (!isValidFormat(jti)) {
          return { name: 'invalid-verify' };
        }
      }
    },
    {
      path: '/verify',
      name: 'invalid-verify',
      component: InvalidVerification
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'invalid',
      component: InvalidVerification
    }
  ]
})

export default router
