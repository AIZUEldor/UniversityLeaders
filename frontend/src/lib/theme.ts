// Plain module (not "use client"): the root server layout inlines THEME_SCRIPT, the Preferences control toggles the theme.
export type Theme = "light" | "dark";
export const THEME_KEY = "theme";

/** Runs in <head> before paint: saved choice, else the system setting. Sets html[data-theme] (see globals.css). */
export const THEME_SCRIPT = `(function(){var t;try{t=localStorage.getItem("${THEME_KEY}");}catch(e){}if(t!=="light"&&t!=="dark")t=window.matchMedia("(prefers-color-scheme: dark)").matches?"dark":"light";document.documentElement.dataset.theme=t;})();`;
