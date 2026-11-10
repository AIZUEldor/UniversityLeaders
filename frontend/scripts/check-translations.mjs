// Lists Uzbek UI strings in src/ that have no ru or en entry in src/lib/translations.ts. Exit code 1 when any is missing.
// Usage: node scripts/check-translations.mjs [--keys]  (--keys prints every candidate key, for filling the dictionary)
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";

const SRC = new URL("../src/", import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1");
const files = [];
(function walk(dir) {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) walk(path);
    else if (/\.(tsx?|mjs)$/.test(name) && !path.endsWith("translations.ts")) files.push(path);
  }
})(SRC);

// Candidate UI text: string literals and <T> children that read like Uzbek prose, not class names, paths or code.
const looksLikeText = (s) =>
  /[A-Za-zʻ']/.test(s) && /[a-z]/.test(s) && !/^[a-z0-9_:./@#\-[\]()%=&?*,+ ]+$/.test(s) // class lists, paths, ids
  && !/^(use client|use server)$/.test(s) && !/[{}<>;]|=>|\$\{/.test(s.replace(/\{\w+\}/g, ""))
  && !/^(GET|POST|PUT|DELETE|Draft|Submitted|Approved|Rejected|[A-Z][a-z]+[A-Z]\w*)$/.test(s)
  && !/^[A-Za-z]+$/.test(s) || /^(Login|Parol|Kirish|Reyting|Fakultet|Fakultetlar|Natija|Tavsif|Ball|Holat|Sabab|Matn|Sarlavha|Nomi|Rahbar|Amallar|Tanlang|Yangi|Chiqish|Kabinet|Menyu|Til|Mezon|Universitet|Ishtirokchilar|Yangilangan|Yuborilganlar|Nusxalash|Yopish|Oldingi|Keyingi|Tahrirlash|Saqlash|Yaratish|Yuborish|Tasdiqlash|Tasdiqlangan|Nofaol|nofaol|band|ball|ommaviy|Qoralama|Bildirishnomalar|Fakultetlar)$/.test(s);

const keys = new Set();
const skipLine = /^\s*(import |export \* |\/\/|\*|\/\*)|console\.|throw new (ApiError|Error)\(|new ApiError\(|useAuth must|dataset|localStorage|matchMedia|fieldErrors|\.code ===|case "/;
for (const file of files) {
  for (const line of readFileSync(file, "utf8").split("\n")) {
    if (skipLine.test(line)) continue;
    for (const m of line.matchAll(/"((?:[^"\\]|\\.)*)"/g)) {
      const s = m[1].replace(/\\'/g, "'").replace(/\\"/g, '"');
      if (looksLikeText(s)) keys.add(s);
    }
    for (const m of line.matchAll(/<T>([^<{]+)<\/T>/g)) keys.add(m[1].trim());
  }
}
// describeError messages sit on "case" lines' next line; collect return "..." strings from auth.tsx separately.
for (const m of readFileSync(join(SRC, "lib/auth.tsx"), "utf8").matchAll(/return "((?:[^"\\]|\\.)*)"/g)) keys.add(m[1]);

const dict = readFileSync(join(SRC, "lib/translations.ts"), "utf8");
const has = (lang, key) => {
  const block = dict.split(new RegExp(`^const ${lang}: Dictionary = \\{`, "m"))[1]?.split(/^\};/m)[0] ?? "";
  return block.includes(JSON.stringify(key) + ":");
};

if (process.argv.includes("--keys")) { for (const k of [...keys].sort()) console.log(JSON.stringify(k)); process.exit(0); }
// Not UI text: import paths, headers, SVG data, the language names, locale ids and the HTML metadata description.
const IGNORE = new Set(["@/lib/adminApi", "Content-Type", "O'zb", "uz-UZ", "University Leaders Platform"]);
const missing = [...keys].filter((k) => !IGNORE.has(k) && !k.startsWith("M12 ") && (!has("ru", k) || !has("en", k)));
for (const k of missing) console.log("missing:", JSON.stringify(k));
console.log(`${keys.size} keys, ${missing.length} missing`);
process.exit(missing.length ? 1 : 0);
