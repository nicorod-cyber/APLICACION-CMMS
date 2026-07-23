import { readFile, readdir } from "node:fs/promises";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../..", import.meta.url));
const ignored = new Set([".git", "bin", "obj", "dist", "node_modules", "artifacts", ".codex-tmp", "logs"]);
const extensions = new Set([".cs", ".ts", ".tsx", ".js", ".mjs", ".json", ".yml", ".yaml"]);
const replacement = String.fromCodePoint(0xfffd);
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
  const text = await readFile(path, "utf8");
  if (suspicious.some((value) => text.includes(value))) failures.push(relative(root, path));
}

if (failures.length) {
  console.error(`Se detectó codificación dañada en:\n${failures.join("\n")}`);
  process.exitCode = 1;
}
