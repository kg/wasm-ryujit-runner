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

    const memory = new WebAssembly.Memory({
        initial: 256,
    });

    const stackPointer = new WebAssembly.Global({
        value: "i32",
        mutable: true,
    }, 4096);
    const imageBase = new WebAssembly.Global({
        value: "i32",
        mutable: false,
    }, stackPointer.value);
    const imagePointerBase = new WebAssembly.Global({
        value: "i32",
        mutable: false,
    }, 0);
    const imports = {
        env: {
            memory: memory,
            __stack_pointer: stackPointer,
            __image_base: imageBase,
            __image_function_pointer_base: imagePointerBase,
        },
    };

    const instance = await WebAssembly.instantiate(module, imports);
    console.log(`OK!`);
    console.log(`exports=${JSON.stringify(Object.keys(instance.exports))}`);
    const jsExpression = process.argv[3];
    console.log(`running '${jsExpression}...`);
    const testModule = process.argv[4];

    function doEval(module, instance, exports) {
        // FIXME: Handle growth during execution
        const HEAPU8 = new Uint8Array(memory.buffer);
        const HEAPU16 = new Uint16Array(memory.buffer);
        const HEAPU32 = new Uint32Array(memory.buffer);
        const HEAPI8 = new Int8Array(memory.buffer);
        const HEAPI16 = new Int16Array(memory.buffer);
        const HEAPI32 = new Int32Array(memory.buffer);

        debugger;

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