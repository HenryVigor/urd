import { createRouter, createWebHashHistory } from 'vue-router'
import { handleHotUpdate, routes } from 'vue-router/auto-routes'

const router = createRouter({
    history: createWebHashHistory(),
    routes: [
        // Use auto routes, but redirect to folder index for starting page
        { path: '/', redirect: '/folders' },
        ...routes.filter((route) => route.path !== '/'),
    ],
})

if (import.meta.hot) {
    handleHotUpdate(router)
}

export default router
