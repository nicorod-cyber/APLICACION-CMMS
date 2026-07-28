import { readFile, readdir } from "node:fs/promises";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../..", import.meta.url));
const ignored = new Set([".git", "bin", "obj", "dist", "node_modules", "artifacts", ".codex-tmp", "logs"]);
const extensions = new Set([".cs", ".ts", ".tsx", ".js", ".mjs", ".json", ".yml", ".yaml"]);
const replacement = String.fromCodePoint(0xfffd);
const damagedSpanish = /\b(?:f\?sico|fotograf\?a|descripci\?n|hist\?rico|composici\?n|f\?brica|medici\?n)\b/i;
const literalPowerShellNewline = String.fromCharCode(96) + "r" + String.fromCharCode(96) + "n";
const suspicious = [
  replacement,
  "\u00c3\u0192",
  "\u00c2",
  "\u00e2\u20ac",
  "\u00c3\u00af\u00c2\u00bf\u00c2\u00bd"
];

async function files(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const nested = await Promise.all(entries.map(async (entry) => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return ignored.has(entry.name) ? [] : files(path);
    return extensions.has(entry.name.slice(entry.name.lastIndexOf("."))) ? [path] : [];
  }));
  return nested.flat();
}

const failures = [];
for (const path of await files(root)) {
  const bytes = await readFile(path);
  const text = bytes.toString("utf8");
  const hasBom = [".ts", ".tsx"].some((extension) => path.endsWith(extension)) && bytes.subarray(0, 3).equals(Buffer.from([0xef, 0xbb, 0xbf]));
  if (suspicious.some((value) => text.includes(value)) || damagedSpanish.test(text) || text.includes(literalPowerShellNewline)) failures.push(relative(root, path));
}

if (failures.length) {
  console.error(`Se detectó codificación dañada en:\n${failures.join("\n")}`);
  process.exitCode = 1;
}
