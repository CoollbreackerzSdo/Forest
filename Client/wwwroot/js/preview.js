/* ==========================================================================
   MAREA · local previews
   --------------------------------------------------------------------------
   The API stores the profile photo and the banner and returns their
   identifiers, so the real images are painted from the server (img/{id}).
   This module only covers the gap between picking a file and saving it: it
   makes an object URL out of the file the browser already holds in the
   <input type="file">, so the avatar and the cover show it right away.
   ========================================================================== */

/** Returns a revocable object URL for the file selected in an input. */
export function objectUrl(inputId) {
    const input = document.getElementById(inputId);
    const file = input && input.files && input.files[0];
    if (!file) return null;

    if (input.dataset.preview) URL.revokeObjectURL(input.dataset.preview);

    const url = URL.createObjectURL(file);
    input.dataset.preview = url;
    return url;
}

/** Releases the object URL of an input, if any. */
export function revoke(inputId) {
    const input = document.getElementById(inputId);
    if (!input || !input.dataset.preview) return;

    URL.revokeObjectURL(input.dataset.preview);
    delete input.dataset.preview;
}
