#!/usr/bin/env node

import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

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
    const testModule = process.argv[4];

    function doEval(module, instance, exports) {
        const result = eval(jsExpression);
        console.log(`result was ${JSON.stringify(result)}`);
    }

    if (testModule !== '') {
        const testModuleUrl = pathToFileURL(resolve(testModule)).href;
        import (testModuleUrl).then((mod) => { 
            console.log(`test module '${testModuleUrl}' loaded: ${Object.keys(mod)}`);
            // Make the test module's exports available in the eval context
            Object.assign(globalThis, mod);
            doEval(module, instance, instance.exports);
        }).catch((err) => {
            console.error(`error loading test module '${testModuleUrl}': ${err}`);
        });
    } else {
        doEval(module, instance, instance.exports);
    }

    debugger;
} catch (err) {
    debugger;
    throw err;
}