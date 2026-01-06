#!/usr/bin/env dotnet
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#:package System.CommandLine@2.0.0

using System;
using System.Text;
using System.CommandLine;
using System.Diagnostics;

Option<string> oConfiguration = new("--config") {
    Description = "The runtime build configuration to use."
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
Option<FileInfo> oR2RPath = new("--r2r-path") {
    Description = "The location of the R2R binary."
};
Option<FileInfo> oAssembly = new("--assembly") {
    Description = "The assembly to R2R compile."
};

RootCommand rootCommand = new("Wasm RyuJIT Simple Test Harness");
rootCommand.Options.Add(oConfiguration);
rootCommand.Options.Add(oCheckout);
rootCommand.Options.Add(oTempDir);
rootCommand.Options.Add(oKeepTempDir);
rootCommand.Options.Add(oR2RPath);
rootCommand.Options.Add(oAssembly);

ParseResult options = rootCommand.Parse(args);
if (options.Errors.Count != 0) {
    foreach (var parseError in options.Errors) {
        Console.Error.WriteLine(parseError.Message);
    }
    return 1;
}

var configuration = options.GetValue(oConfiguration) ?? "Debug";
var checkout = options.GetValue(oCheckout)?.FullName ?? Environment.CurrentDirectory;
var osName = "windows"; // FIXME
var archName = "x64"; // FIXME
var crossgenPath = Path.Combine(checkout, "artifacts", "bin", "coreclr", $"{osName}.{archName}.{configuration}", archName, "crossgen2", "crossgen2.exe");
if (!File.Exists(crossgenPath))
    throw new FileNotFoundException($"Not found - make sure to pass --checkout: {crossgenPath}");

var tempDir = options.GetValue(oTempDir)?.FullName;
var keepTempDir = (tempDir != null) && Directory.Exists(tempDir);
if (tempDir == null) {
    tempDir = Path.GetTempFileName();
    File.Delete(tempDir);
    Directory.CreateDirectory(tempDir);
    keepTempDir = options.GetValue(oKeepTempDir);
}

try {
    var outPath = Path.Combine(tempDir, "test-module.wasm");
    File.Delete(outPath);

    var assemblyPath = options.GetValue(oAssembly)?.FullName;
    if (!File.Exists(assemblyPath))
        throw new FileNotFoundException($"Not found - make sure to pass --assembly: {assemblyPath}");

    var rspPath = Path.Combine(tempDir, "cg2.rsp");
    Console.WriteLine($"/// Generate '{rspPath}'...");
    using (var sw = new StreamWriter(rspPath, false, Encoding.UTF8)) {
        sw.WriteLine(@"--verbose
--print-repro-instructions
--targetos:browser
--targetarch:wasm
--obj-format=wasm");

        sw.Write("-r:\"");
        sw.Write(Path.Combine(checkout, "artifacts", "tests", "coreclr", $"browser.wasm.{configuration}", "Tests", "Core_Root", "*.dll"));
        sw.WriteLine("\"");

        sw.Write("--out:\"");
        sw.Write(outPath);
        sw.WriteLine("\"");

        sw.Write('"');
        sw.Write(assemblyPath);
        sw.WriteLine('"');
    }

    Console.WriteLine($"/// Run '{crossgenPath} @{rspPath}'...");
    await RunChildProcess(crossgenPath, "@" + rspPath);

    return 0;
} finally {
    if (!keepTempDir) {
        Console.WriteLine($"/// Delete '{tempDir}'...");
        Directory.Delete(tempDir, true);
    }
}

static async Task RunChildProcess (string process, string args) {
    var proc = new Process() {
        StartInfo = {
            FileName = process,
            Arguments = args,
            UseShellExecute = false,
            // CreateNoWindow = true,
        },
    };

    proc.Start();
    await proc.WaitForExitAsync();

    if (proc.ExitCode != 0)
        throw new Exception($"Child process failed with exit code {proc.ExitCode}");
}