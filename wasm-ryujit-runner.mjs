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
    console.log(`exports=${JSON.stringify(Object.keys(instance.exports))}`);
    const jsExpression = process.argv[3];
    console.log(`running '${jsExpression}...`);
    doEval(module, instance, instance.exports);
    function doEval (module, instance, exports) {
        const result = eval(jsExpression);
        console.log(`result was ${JSON.stringify(result)}`);
    }
    debugger;
} catch (err) {
    debugger;
    throw err;
}