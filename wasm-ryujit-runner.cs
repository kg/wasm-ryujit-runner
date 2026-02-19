#!/usr/bin/env dotnet
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#:package System.CommandLine@2.0.0

using System;
using System.Text;
using System.CommandLine;
using System.Diagnostics;
using System.Runtime.CompilerServices;

Option<string> oConfiguration = new("--config") {
    Description = "The runtime build configuration to use."
};
Option<string> oJsExpr = new("--js-expr") {
    Description = "The javascript expression to evaluate on the loaded module. `module`, `instance` and `exports` are available."
};
Option<string> oTestModule = new("--test-module") {
    Description = "The test JS module to load."
};  
Option<DirectoryInfo> oCheckout = new("--checkout") {
    Description = "The location of your .NET checkout."
};
Option<DirectoryInfo> oTempDir = new("--temp-dir") {
    Description = "The temporary directory to use."
};
Option<bool> oKeepTempDir = new("--keep-temp-dir") {
    Description = "If the temporary directory is automatically generated, keep it around after running."
};
Option<bool> oAutoBuild = new("--auto-build") {
    Description = "Automatically perform builds if necessary."
};
Option<bool> oAlwaysBuild = new("--always-build") {
    Description = "Always build the clr+libs subset."
};
Option<bool> oDisasm = new("--disasm") {
    Description = "Ask node/v8 to disassemble wasm functions when compiling them."
};
Option<bool> oInspect = new("--inspect") {
    Description = "Pass the --inspect switch to node, enabling debugging."
};
Option<FileInfo> oR2RPath = new("--r2r-path") {
    Description = "The location of the R2R binary."
};
Option<FileInfo> oTestHarnessPath = new("--test-harness-path") {
    Description = "The location of the test harness."
};
Option<FileInfo> oAssembly = new("--assembly") {
    Description = "The assembly to R2R compile."
};

RootCommand rootCommand = new("Wasm RyuJIT Simple Test Harness");
rootCommand.Options.Add(oConfiguration);
rootCommand.Options.Add(oJsExpr);
rootCommand.Options.Add(oTestModule);
rootCommand.Options.Add(oCheckout);
rootCommand.Options.Add(oTempDir);
rootCommand.Options.Add(oKeepTempDir);
rootCommand.Options.Add(oAutoBuild);
rootCommand.Options.Add(oAlwaysBuild);
rootCommand.Options.Add(oDisasm);
rootCommand.Options.Add(oInspect);
rootCommand.Options.Add(oR2RPath);
rootCommand.Options.Add(oTestHarnessPath);
rootCommand.Options.Add(oAssembly);

ParseResult options = rootCommand.Parse(args);
if (options.Errors.Count != 0) {
    foreach (var parseError in options.Errors)
        Log(parseError.Message);
    return 1;
}

int sclExitCode = await options.InvokeAsync();
if (sclExitCode != 0)
    return sclExitCode;

var alwaysBuild = options.GetValue(oAlwaysBuild);
var autoBuild = options.GetValue(oAutoBuild) || alwaysBuild;
var configuration = options.GetValue(oConfiguration) ?? "Debug";
var checkout = options.GetValue(oCheckout)?.FullName ?? Environment.CurrentDirectory;
var osName = "windows"; // FIXME
var archName = "x64"; // FIXME
var crossgenPath = options.GetValue(oR2RPath)?.FullName ??
    Path.Combine(checkout, "artifacts", "bin", "coreclr", $"{osName}.{archName}.{configuration}", archName, "crossgen2", "crossgen2.exe");

var assemblyPath = options.GetValue(oAssembly)?.FullName;
if (!File.Exists(assemblyPath))
    throw new FileNotFoundException($"Not found - make sure to pass --assembly: {assemblyPath}");

if (alwaysBuild || !File.Exists(crossgenPath)) {
    if (!autoBuild && !alwaysBuild)
        throw new FileNotFoundException($"Not found - maybe pass --checkout: {crossgenPath}");

    if (Directory.Exists(Path.Combine(checkout, "src", "coreclr", "jit"))) {
        if (!alwaysBuild)
            Log($"/// Not found: '{crossgenPath}'. Attempting to build clr+libs to get a crossgen2 binary...");
        await RunChildProcess(Path.Combine(checkout, "build.cmd"), $"-c {configuration} -lc Release clr+libs", checkout);

        if (!File.Exists(crossgenPath))
            throw new FileNotFoundException($"Build did not produce a crossgen2 binary!");
    } else
        throw new Exception($"Path does not appear to be a runtime checkout, Maybe pass --checkout: {checkout}");
}

var coreRootPath = Path.Combine(checkout, "artifacts", "tests", "coreclr", $"browser.wasm.{configuration}", "Tests", "Core_Root");
if (!Directory.Exists(coreRootPath) || Directory.GetFiles(coreRootPath, "*.dll").Length == 0) {
    var msg = $"Not found: '{coreRootPath}\\*.dll'.";
    if (!autoBuild)
        throw new FileNotFoundException(msg);

    Log("/// " + msg + " Attempting to build clr+libs for browser...");
    await RunChildProcess(Path.Combine(checkout, "build.cmd"), $"-c {configuration} -lc Release -os browser clr+libs", checkout);

    Log("/// Attempting to build browser core_root...");
    await RunChildProcess(Path.Combine(checkout, "src", "tests", "build.cmd"), $"wasm browser generatelayoutonly", Path.Combine(checkout, "src", "tests"));

    if (!Directory.Exists(coreRootPath) || Directory.GetFiles(coreRootPath, "*.dll").Length == 0)
        throw new Exception($"Build failed to generate core_root at {coreRootPath}!");
}

var tempDir = options.GetValue(oTempDir)?.FullName;
var keepTempDir = (tempDir != null) && Directory.Exists(tempDir);
if (tempDir == null) {
    tempDir = Path.GetTempFileName();
    File.Delete(tempDir);
    Directory.CreateDirectory(tempDir);
    keepTempDir = options.GetValue(oKeepTempDir);
}

try {
    var outName = "test-module.wasm";
    var outPath = Path.Combine(tempDir, outName);
    File.Delete(outPath);

    var rspPath = Path.Combine(tempDir, "cg2.rsp");
    Log($"/// Generate '{rspPath}'...");
    using (var sw = new StreamWriter(rspPath, false, Encoding.UTF8)) {
        sw.WriteLine(@"--verbose
--print-repro-instructions
--targetos:browser
--targetarch:wasm
--obj-format=wasm");

        sw.Write("-r:\"");
        sw.Write(Path.Combine(coreRootPath, "*.dll"));
        sw.WriteLine("\"");

        sw.Write("--out:\"");
        sw.Write(outPath);
        sw.WriteLine("\"");

        sw.Write('"');
        sw.Write(assemblyPath);
        sw.WriteLine('"');
    }

    await RunChildProcess(crossgenPath, "@" + rspPath, tempDir);

    if (!File.Exists(outPath))
        throw new FileNotFoundException($"Crossgen did not generate '{outPath}'!");
    else
        Log($"/// '{outPath}' generated. Starting test harness...");

    var testHarnessPath = options.GetValue(oTestHarnessPath)?.FullName ??
        Path.Combine(Path.GetDirectoryName(GetMySourceFilePath()), "wasm-ryujit-runner.mjs");
    if (!File.Exists(testHarnessPath))
        throw new FileNotFoundException($"Test harness not found - maybe pass --test-harness-path: {testHarnessPath}");

    var nodeArgs = "";
    if (options.GetValue(oDisasm))
        nodeArgs += " --print-wasm-code --no-liftoff";
    if (options.GetValue(oInspect))
        nodeArgs += " --inspect --inspect-wait";

    var jsExpr = options.GetValue(oJsExpr) ?? "";
    var jsTestModule = options.GetValue(oTestModule) ?? "";

    await RunChildProcess("node", $"{nodeArgs} \"{testHarnessPath}\" {outName} \"{jsExpr}\" \"{jsTestModule}\"", tempDir);

    return 0;
} finally {
    if (!keepTempDir) {
        Log($"/// Delete '{tempDir}'...");
        Directory.Delete(tempDir, true);
    }
}

static string GetMySourceFilePath ([CallerFilePath]string filePath = "") =>
    filePath;

static void Log (string text) {
    var oldColor = Console.ForegroundColor;
    try {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Error.WriteLine(text);
    } finally {
        Console.ForegroundColor = oldColor;
    }
}

static async Task RunChildProcess (string process, string args, string cwd = "") {
    var proc = new Process() {
        StartInfo = {
            FileName = process,
            Arguments = args,
            UseShellExecute = false,
            // CreateNoWindow = true,
            WorkingDirectory = cwd ?? "",
        },
    };

    Log($"/// Run '\"{proc.StartInfo.FileName}\" {proc.StartInfo.Arguments}' in cwd '{cwd}'...");
    proc.Start();
    await proc.WaitForExitAsync();

    if (proc.ExitCode != 0)
        throw new Exception($"Child process '{process}' failed with exit code {proc.ExitCode}");
}