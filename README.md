# wasm-ryujit-runner

## Requirements

* .NET SDK 10.0 or later
* A test managed assembly
* A source checkout of the .NET runtime repository (to run crossgen2 and generate wasm from your test assembly)
* A recent build of node.js in your PATH (to run tests)
* Edge or Chrome (if you want to debug node.js with `--inspect`)

## How to use

```
dotnet run wasm-ryujit-runner.cs -- --help

dotnet run wasm-ryujit-runner.cs --checkout:Z:\runtime --assembly:Z:\wasm-test\bin\Debug\net9.0\wasm-test.dll --auto-build
```