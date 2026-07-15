# LuaDecompiler

[中文](#中文) · [English](#english)

LuaDecompiler is a standalone Windows desktop application for inspecting, decompiling, previewing, and exporting Lua bytecode files.

---

## 中文

### 简介

LuaDecompiler 是一个独立的 Windows 桌面 Lua 字节码反编译工具。它提供批量导入、版本识别、中文字符串解码、全文件虚拟预览和显式保存流程。反编译结果先写入本次会话的系统临时目录；只有点击保存按钮后，结果才会写入永久目录。

### 特性

- 支持识别 Lua 5.0、5.1、5.2、5.3 和 5.4 字节码。
- 支持 `.lua`、`.luac`、`.lub`、`.bytes` 和 `.out` 文件。
- 支持拖放文件或目录，以及递归批量扫描。
- 自动区分 Lua 源代码与字节码，避免把普通源代码误送入反编译引擎。
- 为一种非标准 Lua 5.3 头部及 opcode 布局提供兼容性规范化，原始文件不会被修改。
- 自动识别 Lua 字符串中的十进制字节转义，并尝试按 UTF-8 或 GBK/GB18030 解码可读中文。
- 逐行读取反编译输出并流式写入会话临时文件，降低大型文件的峰值内存占用。
- 使用按行索引、按可见页读取的虚拟预览器，可通过纵向和横向滚动条浏览完整结果。
- 缓存已打开文件的索引和滚动位置，在多个大型结果之间快速切换。
- 文件列表列宽与左右分栏会随窗口尺寸自动适配，同时保留手动拖动能力。
- 反编译完成后不会自动创建永久输出；点击“保存选中”或“保存全部”后才会写入目标目录。
- 清空列表或正常退出程序时，会删除本次会话的临时结果。

### 运行依赖

| 依赖 | 最低要求 | 用途 |
| --- | --- | --- |
| 操作系统 | Windows 10/11 x64 | 本程序是 64 位 Windows 桌面应用 |
| .NET | [.NET 6 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/6.0) | 运行 `LuaDecompiler.exe`；只需 Desktop Runtime，不需要 ASP.NET Runtime 或 SDK |
| Java | Java Runtime/JRE 8 或更高版本 | 运行随包提供的 `unluac.jar` 反编译引擎 |

Java 可以通过系统 `PATH` 提供 `java.exe`，也可在程序的“引擎设置”中手动选择。没有 Java 时程序界面仍能启动，但不能执行反编译。无需单独下载 `unluac.jar`，它已经包含在发布包中。

### 使用方法

1. 从 [Releases](https://github.com/kanppa/LuaDecompiler/releases) 下载 Windows x64 压缩包，**必须先完整解压到普通文件夹**。
2. 安装 .NET 6 Desktop Runtime x64，并安装 Java 8 或更高版本。
3. 运行 `LuaDecompiler.exe`。
4. 点击“添加文件”或“添加文件夹”，也可以直接把文件或目录拖入窗口。
5. 选择文件后点击“反编译选中”，或者点击“全部反编译”。
6. 在“源码预览”中使用滚动条、方向键、`Page Up`、`Page Down`、`Ctrl+Home` 和 `Ctrl+End` 浏览完整结果。
7. 需要永久保留结果时，点击“保存选中”或“保存全部”。默认保存目录是当前 `LuaDecompiler.exe` 同级的 `Decompiled` 子目录。

### 从源码编译

构建环境需要：

- Windows 10/11 x64。
- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)。
- Windows PowerShell 5.1 或 PowerShell 7 及更高版本。
- Java Runtime/JRE 8 或更高版本，用于运行和验证反编译功能。
- Git（仅在使用下方 `git clone` 命令时需要）。

```powershell
git clone https://github.com/kanppa/LuaDecompiler.git
cd LuaDecompiler
./build.ps1
```

默认输出：

- `artifacts/app/`：可运行程序目录。
- `artifacts/LuaDecompiler-v0.0.1-win-x64.zip`：Windows x64 发布包。

也可以指定输出根目录：

```powershell
./build.ps1 -OutputRoot C:\BuildOutput
```

运行测试：

```powershell
dotnet run --configuration Release --project tests/DecoderSmoke/DecoderSmoke.csproj
```

### 注意事项

- 反编译只能恢复语义上等价的 Lua 结构。
- 编译时被移除的注释、调试信息和局部变量名通常无法恢复。
- 不同 Lua 编译器、修改版虚拟机、加密字节码或容器格式可能需要额外兼容处理。
- 会话临时文件位于 `%TEMP%\LuaDecompiler\session-*`，正常退出或清空列表时会被删除。
- 默认保存目录始终是当前 `LuaDecompiler.exe` 同级的 `Decompiled`；仅当用户主动选择其他目录时，该自定义目录才会跨启动保留。
- 不要直接运行 ZIP 内的 EXE。Windows 可能从临时解压目录启动它，程序会显示先完整解压的提示。

### 第三方组件

反编译引擎使用 [unluac](https://sourceforge.net/projects/unluac/)。其 MIT 许可证位于 `third_party/UNLUAC_LICENSE.txt`，并随发布包分发。

---

## English

### Overview

LuaDecompiler is a standalone Windows desktop application for Lua bytecode. It provides batch import, version detection, escaped-text decoding, full-file virtual preview, and explicit export. Decompiled results are first written to a session-scoped temporary directory and become persistent only after the user clicks a save button.

### Features

- Detects Lua 5.0, 5.1, 5.2, 5.3, and 5.4 bytecode.
- Accepts `.lua`, `.luac`, `.lub`, `.bytes`, and `.out` files.
- Supports drag-and-drop, folders, and recursive batch scanning.
- Distinguishes Lua source files from bytecode before invoking the decompiler.
- Normalizes one known alternate Lua 5.3 header and opcode layout without modifying the input file.
- Detects decimal byte escapes in Lua strings and attempts readable UTF-8 or GBK/GB18030 decoding.
- Streams decompiler output through line-by-line decoding into session files to reduce peak memory usage.
- Uses a line-indexed, page-based virtual viewer, allowing the complete result to be browsed with vertical and horizontal scroll bars.
- Caches file indexes and scroll positions for fast switching between large results.
- Automatically resizes table columns and split panels while preserving manual resizing.
- Does not create persistent output until **Save Selected** or **Save All** is clicked.
- Removes session files when the list is cleared or the application exits normally.

### Runtime dependencies

| Dependency | Minimum requirement | Purpose |
| --- | --- | --- |
| Operating system | Windows 10/11 x64 | The application is a 64-bit Windows desktop program |
| .NET | [.NET 6 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/6.0) | Runs `LuaDecompiler.exe`; the ASP.NET Runtime and SDK are not required |
| Java | Java Runtime/JRE 8 or later | Runs the bundled `unluac.jar` decompiler engine |

`java.exe` may be available through the system `PATH`, or a specific executable can be selected in **Engine Settings**. The interface can open without Java, but decompilation cannot run. `unluac.jar` is already included in the release archive and does not need to be downloaded separately.

### Usage

1. Download the Windows x64 archive from [Releases](https://github.com/kanppa/LuaDecompiler/releases), then **fully extract it to a normal folder before running it**.
2. Install the .NET 6 Desktop Runtime x64 and Java 8 or later.
3. Run `LuaDecompiler.exe`.
4. Add files or a folder, or drag them into the window.
5. Use **Decompile Selected** or **Decompile All**.
6. Browse the complete output in **Source Preview** with the scroll bars, arrow keys, `Page Up`, `Page Down`, `Ctrl+Home`, or `Ctrl+End`.
7. Use **Save Selected** or **Save All** to keep results permanently. The default output directory is `Decompiled` next to the running executable.

### Build from source

The build environment requires:

- Windows 10/11 x64.
- The [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0).
- Windows PowerShell 5.1 or PowerShell 7 or later.
- Java Runtime/JRE 8 or later for running and testing decompilation.
- Git, only when using the `git clone` command below.

```powershell
git clone https://github.com/kanppa/LuaDecompiler.git
cd LuaDecompiler
./build.ps1
```

Default outputs:

- `artifacts/app/` — runnable application directory.
- `artifacts/LuaDecompiler-v0.0.1-win-x64.zip` — Windows x64 release archive.

To use another output root:

```powershell
./build.ps1 -OutputRoot C:\BuildOutput
```

Run the smoke tests:

```powershell
dotnet run --configuration Release --project tests/DecoderSmoke/DecoderSmoke.csproj
```

### Limitations

- Decompilation can only reconstruct semantically equivalent Lua code.
- Comments, debug information, and local names removed during compilation usually cannot be recovered.
- Different compilers, modified virtual machines, encrypted chunks, or container formats may require additional compatibility work.
- Session files are stored under `%TEMP%\LuaDecompiler\session-*` and are removed when the list is cleared or the application exits normally.
- The default output is always `Decompiled` next to the currently running `LuaDecompiler.exe`; only a directory explicitly selected by the user is retained across launches.
- Do not run the EXE directly from inside the ZIP. Windows may run it from a temporary extraction directory, and the application will show an extraction warning.

### Third-party component

The decompiler engine is [unluac](https://sourceforge.net/projects/unluac/). Its MIT license is included at `third_party/UNLUAC_LICENSE.txt` and is distributed with release builds.
