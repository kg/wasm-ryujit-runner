#!/usr/bin/env node

import { readFileSync } from 'node:fs';

try {
    const modulePath = process.argv[2];
    console.log(`Loading ${modulePath}...`);
    const bytes = readFileSync(modulePath);
    console.log(`Compiling ${modulePath}...`);
    const module = await WebAssembly.compile(bytes);
    console.log(`Instantiating ${modulePath}...`);
    const imports = {};
    const instance = await WebAssembly.instantiate(module, imports);
    console.log(`OK!`);
    debugger;
} catch (err) {
    debugger;
    throw err;
}