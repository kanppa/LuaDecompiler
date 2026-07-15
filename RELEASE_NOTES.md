# LuaDecompiler v0.0.1

## 中文

首个公开版本，包含：

- Lua 5.0 至 5.4 字节码识别，以及 `.lua`、`.luac`、`.lub`、`.bytes`、`.out` 文件导入。
- 文件/目录拖放、递归批量反编译和会话临时结果管理。
- UTF-8、GBK/GB18030 中文转义字符串解码。
- 支持完整结果滚动浏览的虚拟预览器，并缓存索引和滚动位置。
- 随窗口自动适配的文件列表和左右分栏布局。
- 默认保存到当前 `LuaDecompiler.exe` 同级的 `Decompiled` 子目录；只有点击保存后才写入永久文件。
- 旧默认路径迁移、自定义保存目录保留和 ZIP 临时目录运行提示。

运行需要 Windows 10/11 x64、.NET 6 Desktop Runtime x64，以及 Java Runtime/JRE 8 或更高版本。Java 用于运行随发布包提供的 `unluac.jar`；没有 Java 时界面可以启动，但无法执行反编译。使用前必须把 ZIP 完整解压到普通文件夹。

## English

Initial public release, including:

- Lua 5.0 through 5.4 bytecode detection and `.lua`, `.luac`, `.lub`, `.bytes`, and `.out` imports.
- File/folder drag-and-drop, recursive batch decompilation, and session-scoped temporary results.
- UTF-8 and GBK/GB18030 escaped-text decoding.
- A virtual viewer that scrolls through complete results and caches indexes and positions.
- A responsive file list and split-pane layout.
- A default `Decompiled` directory next to the running `LuaDecompiler.exe`; persistent files are written only after Save is clicked.
- Migration of old default paths, retention of explicit custom paths, and a warning when launched from a ZIP temporary directory.

Runtime requirements are Windows 10/11 x64, the .NET 6 Desktop Runtime x64, and Java Runtime/JRE 8 or later. Java runs the bundled `unluac.jar`; the interface can open without Java, but decompilation cannot run. Fully extract the ZIP to a normal folder before use.
