/* ==========================================================================
   MAREA · boot screen
   --------------------------------------------------------------------------
   The loading screen lives outside `#app`, so Blazor can take over the page
   while it is still on screen. When the app is ready the screen fades away
   instead of disappearing in a flash.
   ========================================================================== */

const BOOT_ID = 'boot';

/** Fades the loading screen out and takes it out of the DOM. */
export function hideBoot() {
    const el = document.getElementById(BOOT_ID);
    if (!el) return;

    el.classList.add('is-done');
    el.addEventListener('transitionend', () => el.remove(), { once: true });
    setTimeout(() => el.remove(), 1500);
}
