import { build } from 'esbuild';

const common = { entryPoints: ['src/index.ts'], bundle: true, target: 'es2020', sourcemap: true, legalComments: 'none' };

// Script-tag build: window.tarantula is available as soon as the file has loaded.
await build({ ...common, format: 'iife', globalName: 'TarantulaSdk', minify: true, outfile: 'dist/tarantula.min.js' });

// Module build for bundlers (Nuxt, Vite, ...).
await build({ ...common, format: 'esm', outfile: 'dist/tarantula.esm.js' });

console.log('SDK built: dist/tarantula.min.js, dist/tarantula.esm.js');
