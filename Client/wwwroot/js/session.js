/* ==========================================================================
   Cookie de sesión.

   El servidor es el dueño del token: lo guarda en la cookie `auth_token`
   (HttpOnly) al registrarse (`auth/sign-up-web`) y lo borra al cerrar sesión
   (`auth/sign-out-web`). El navegador la envía sola en cada petición, así que
   el cliente no necesita guardar nada.

   Este módulo solo cubre el alta de sesión, porque el API no expone todavía
   `auth/sign-in-web`: el token que devuelve `auth/sign-in` se deja en la misma
   cookie para que a partir de ahí el navegador lo envíe por su cuenta.
   ========================================================================== */

const COOKIE = 'auth_token';

/** ¿El navegador guarda cookies seguras en este origen? */
function secureAllowed() {
    return location.protocol === 'https:'
        || location.hostname === 'localhost'
        || location.hostname === '127.0.0.1';
}

function read() {
    const found = document.cookie.split('; ').find(part => part.startsWith(`${COOKIE}=`));
    return found ? decodeURIComponent(found.slice(COOKIE.length + 1)) : null;
}

/** Guarda el token en la cookie que usa el servidor. */
export function setAuthCookie(token) {
    const value = encodeURIComponent(token);
    const secure = secureAllowed() ? '; Secure' : '';

    document.cookie = `${COOKIE}=${value}; Path=/; SameSite=Lax${secure}`;

    if (read() === null) {
        // el navegador ha rechazado la cookie segura: se deja sin Secure
        document.cookie = `${COOKIE}=${value}; Path=/; SameSite=Lax`;
    }

    return read() !== null;
}

/** Borra la cookie (por si el servidor no pudo hacerlo). */
export function clearAuthCookie() {
    document.cookie = `${COOKIE}=; Path=/; SameSite=Lax; Max-Age=0`;
    document.cookie = `${COOKIE}=; Path=/; SameSite=Lax; Secure; Max-Age=0`;
}

/** ¿Hay sesión? (la cookie es HttpOnly cuando la pone el servidor) */
export function hasAuthCookie() {
    return read() !== null;
}
