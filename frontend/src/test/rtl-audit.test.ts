import fs from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

const PHYSICAL_DIRECTIONAL_CLASS =
  /\b(?:ml|mr|pl|pr|left|right|text-left|text-right|border-l|border-r|rounded-l|rounded-r)-\S+/g;

function walkSourceFiles(dir: string): string[] {
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  const results: string[] = [];
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === 'test' || entry.name === 'types' || entry.name === 'locales') {
        continue;
      }
      results.push(...walkSourceFiles(fullPath));
    } else if (entry.name.endsWith('.ts') || entry.name.endsWith('.tsx')) {
      results.push(fullPath);
    }
  }
  return results;
}

describe('RTL logical CSS property audit', () => {
  it('uses zero physical directional Tailwind classes in frontend/src', () => {
    const srcDir = path.resolve(__dirname, '..');
    const sourceFiles = walkSourceFiles(srcDir);
    const violations: string[] = [];

    for (const file of sourceFiles) {
      const content = fs.readFileSync(file, 'utf8');
      const matches = content.match(PHYSICAL_DIRECTIONAL_CLASS);
      if (matches) {
        violations.push(`${path.relative(srcDir, file)}: ${matches.join(', ')}`);
      }
    }

    expect(violations).toEqual([]);
  });
});
