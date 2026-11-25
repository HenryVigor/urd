// TODO: Remove this file if unused
const rails = new Set()
let animationFrame = null

export function registerRail(update) {
    rails.add(update)

    if (rails.size === 1) {
        window.addEventListener('scroll', schedulePositionUpdate, { capture: true, passive: true })
        window.addEventListener('resize', schedulePositionUpdate, { passive: true })
    }

    schedulePositionUpdate()
}

export function unregisterRail(update) {
    rails.delete(update)

    if (rails.size) return

    window.removeEventListener('scroll', schedulePositionUpdate, { capture: true })
    window.removeEventListener('resize', schedulePositionUpdate)

    if (animationFrame !== null) {
        window.cancelAnimationFrame(animationFrame)
        animationFrame = null
    }
}

export function schedulePositionUpdate() {
    if (animationFrame !== null) return

    animationFrame = window.requestAnimationFrame(() => {
        animationFrame = null
        rails.forEach((update) => update())
    })
}
