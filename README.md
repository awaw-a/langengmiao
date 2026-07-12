# 烂梗喵

烂梗喵是一个 Windows 桌面工具：从 [sb6657](https://sb6657.cn) 获取随机烂梗，并通过 CS2 原生 CFG 命令发送到全体聊天或队内聊天。

发送时不会弹出聊天栏，不占用剪贴板，也不会模拟 `Y`、`U` 或 `Enter`。当前仅支持 Windows x64。

## 功能

- Godot 4 C# 图形化界面
- 自动检测 Steam 与 CS2 安装目录，也支持手动选择
- 点击按钮后直接按键来设置游戏内发送键，默认 `F8`
- 支持全体聊天和队内聊天
- 通过 CS2 原生 `say` / `say_team` 命令直接发送
- 预取并缓存多条烂梗，发送后自动准备下一条
- 安全更新当前 Steam 用户的 CS2 按键配置
- 自动备份配置，换绑时恢复旧按键原有命令
- 一键删除烂梗喵创建的 CFG，不删除其他无关 CFG
- 接口连接测试、发送历史和本地配置持久化

## 下载与运行

1. 打开项目的 [Releases](https://github.com/awaw-a/langengmiao/releases) 页面。
2. 下载最新版本的 `langengmiao-<版本>-windows-x64.zip`；不要只下载单独的源码压缩包。
3. 可选：同时下载对应的 `.zip.sha256` 文件，并在 PowerShell 中检查 ZIP：

   ```powershell
   Get-FileHash .\langengmiao-<版本>-windows-x64.zip -Algorithm SHA256
   Get-Content .\langengmiao-<版本>-windows-x64.zip.sha256
   ```

   两处显示的 SHA-256 值应一致。
4. 将 ZIP 完整解压到一个普通文件夹，不要直接在压缩包预览窗口中运行。
5. 双击 `Lanmian.exe`。

当前发行版没有代码签名，Windows SmartScreen 可能显示未知发布者。请只使用本仓库 Releases 提供的文件，并在继续运行前核对 SHA-256。

## 使用前准备

- Steam 已登录需要使用的账号。
- CS2 至少成功启动过一次，以生成该账号的按键配置文件。
- 首次应用绑定、修改已安装的绑定或删除绑定时，必须完全关闭 CS2。
- 获取烂梗和自动补充下一条需要网络连接及 sb6657 服务可用。

## 首次使用

### 1. 确认 CS2 路径

启动后，程序会依次尝试：

1. 读取上次保存的有效路径；
2. 从正在运行的 `cs2.exe` 定位安装目录；
3. 从 Steam 注册表信息和 `libraryfolders.vdf` 查找各个 Steam 库。

典型安装目录如下：

```text
D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive
```

如果自动检测失败，点击“手动选择目录”。可以选择 CS2 安装根目录，也可以选择其下的 `game`、`game\csgo` 或 `game\csgo\cfg`；程序会通过 `game\csgo\pak01_dir.vpk` 校验并还原出安装根目录。无效选择不会覆盖先前保存的有效路径。

### 2. 选择游戏内发送键

1. 点击“当前：F8（点击后按键）”。
2. 按下想使用的键盘按键或鼠标侧键。
3. 界面显示新的按键名称后即表示捕获成功，无需手动输入键名。

如果所选按键在 CS2 中原本已有命令，烂梗喵会在应用绑定前记录该命令，并在换绑或删除时尝试恢复。仍建议选择一个不影响常用操作的按键。

### 3. 设置发送范围和缓存

| 选项 | 作用 |
| --- | --- |
| 全体聊天 | 使用 `say`，对局内所有玩家可见 |
| 队内聊天 | 使用 `say_team`，仅队友可见 |
| 后台缓存数量 | 预先获取的烂梗数量，范围为 2–32，默认 8 |
| 启用发送后自动补充下一条 | 检测到发送键后，把下一条烂梗写入 CFG |
| 保存设置 | 保存发送键、范围和缓存数量；若 CFG 已安装，还会更新游戏绑定 |
| 测试接口连接 | 单独请求一次 sb6657，用于检查网络和后端状态 |

关闭“启用发送后自动补充下一条”只会停止后台换梗，不会解除 CS2 中已有的发送绑定。此时按键仍会发送当前 CFG 中的同一条内容。

### 4. 应用 CFG 绑定

1. 完全退出 CS2，而不只是退回游戏主菜单。
2. 确认界面已经显示一条待发送烂梗。
3. 点击“应用 CFG 绑定”。
4. 等待顶部状态提示绑定完成。
5. 再启动 CS2。

程序会优先选择 Steam `loginusers.vdf` 中标记为最近使用、且存在 CS2 配置的账号；无法确定时，会使用最近修改过 CS2 按键配置的账号。

绑定完成后无需在游戏控制台执行 `exec lanmian_bootstrap`，也无需添加 `-allow_third_party_software`。

### 5. 在游戏中发送

保持烂梗喵运行，并让 CS2 位于前台。按下设置的发送键后：

1. CS2 立即执行 `exec lanmian_send`，直接发送当前烂梗；
2. 烂梗喵检测到同一个物理按键；
3. 稍后将缓存中的下一条烂梗写入 `lanmian_send.cfg`；
4. 下次按键时发送新内容。

游戏内发送本身由 CS2 完成。烂梗喵的后台按键监听只负责记录本次发送并准备下一条，而且仅在 CS2 是前台窗口时响应。

## 日常使用与换绑

- CFG 已安装时，启动烂梗喵会自动获取并写入第一条待发送烂梗；通常不需要再次点击“应用 CFG 绑定”。
- 点击“换一条”会跳过当前预览，并在 CFG 已安装时同步更新下一条发送内容。
- 烂梗喵未运行时，CS2 中的绑定仍然存在，但会重复发送最后写入 CFG 的内容，无法自动换梗。
- 修改发送键或发送范围前先关闭 CS2。保存设置后，程序会更新现有 CFG 和 Steam 用户按键配置。
- Steam 或 CS2 移动到其他磁盘后，使用“重新检测”或“手动选择目录”更新路径。

## 发送机制

本项目不注入 CS2，不读取游戏进程内存，也不向游戏窗口模拟聊天输入。它会管理以下内容：

- `lanmian_send.cfg`：保存当前一条 `say` 或 `say_team` 命令；
- `lanmian_bootstrap.cfg`：将所选按键绑定为 `exec lanmian_send`；
- `autoexec.cfg`：仅添加由明确起止标记包围的烂梗喵托管区；
- 当前 Steam 账号的 CS2 用户按键配置：写入同一条 `exec lanmian_send` 绑定，确保下次启动游戏时直接生效。

文本写入 CFG 前会移除控制字符、把换行合并为空格、替换可能分隔命令的分号，并限制最大长度，以避免烂梗内容被解释为额外控制台命令。

## 配置、日志与备份位置

| 内容 | 默认位置或命名 |
| --- | --- |
| 应用设置 | `%APPDATA%\Godot\app_userdata\烂梗喵\config.json` |
| 应用日志 | `%APPDATA%\Godot\app_userdata\烂梗喵\lanmian.log` |
| 发送 CFG | `<CS2>\game\csgo\cfg\lanmian_send.cfg` |
| 启动绑定 CFG | `<CS2>\game\csgo\cfg\lanmian_bootstrap.cfg` |
| 自动执行配置 | `<CS2>\game\csgo\cfg\autoexec.cfg` |
| Steam 用户按键配置 | `<Steam>\userdata\<账号ID>\730\local\cfg\cs2_user_keys_0_slot0.vcfg` 等 |
| 首次写入备份 | 原文件旁的 `.lanmian.bak` 文件 |

烂梗喵只会覆盖带有自身所有权标记的同名 CFG。如果检测到用户自己创建的同名文件，会停止操作并显示错误，不会强行覆盖。

## 删除绑定与卸载

安全移除步骤：

1. 完全关闭 CS2。
2. 启动烂梗喵并确认 CS2 路径正确。
3. 点击“一键删除烂梗喵 CFG”。
4. 确认状态提示清理完成后，再删除烂梗喵程序目录。

“一键删除烂梗喵 CFG”只会：

- 删除命令严格等于 `exec lanmian_send` 的用户按键绑定；
- 恢复程序记录的新键原有命令；
- 移除 `autoexec.cfg` 中烂梗喵的托管区；
- 删除带烂梗喵所有权标记的 `lanmian_bootstrap.cfg` 和 `lanmian_send.cfg`。

它不会删除其他 CFG、其他按键或 `.lanmian.bak` 备份。保留备份是为了在异常情况下仍可手动恢复。

## 常见问题

### 无法检测到 CS2

先点击“重新检测”。仍然失败时，点击“手动选择目录”，选择 `Counter-Strike Global Offensive` 安装根目录或其下的 `game\csgo\cfg`。如果目录中不存在 `game\csgo\pak01_dir.vpk`，程序会认为它不是有效安装目录。

### 提示“请先关闭 CS2”

应用、换绑和删除配置时必须完全退出 `cs2.exe`，否则游戏退出时可能用内存中的旧设置覆盖新配置。关闭游戏后重试即可。

### 提示找不到 Steam 用户按键配置

确认 Steam 已登录正确账号，并使用该账号至少启动过一次 CS2。退出游戏后重新点击“应用 CFG 绑定”。

### 新按键在游戏中不生效

关闭 CS2，在烂梗喵中重新选择按键并点击“应用 CFG 绑定”，然后重新启动游戏。不要只修改设置后继续使用已经运行中的 CS2。

### 一直发送同一条烂梗

确认烂梗喵保持运行、“启用发送后自动补充下一条”已开启、CS2 是前台窗口，并且接口连接正常。关闭自动补充或退出烂梗喵后，重复发送最后一条属于预期行为。

### 无法获取烂梗

点击“测试接口连接”。如果失败，请检查网络、防火墙和 sb6657 服务状态。已经写入 CFG 的最后一条内容仍可能发送，但无法获取新内容。

### Windows 阻止运行

当前程序没有代码签名。请确认文件来自本仓库 Releases，并核对 SHA-256；不要运行第三方重新打包的版本。

## 使用限制与安全提示

- 自动化聊天可能受到 Steam、服务器或赛事规则限制，请先在离线、私人或明确允许的环境中测试。
- 烂梗内容来自第三方服务，发送前请查看界面中的待发送内容并自行判断是否合适。
- 不建议将发送键设置为移动、射击、购买菜单等高频游戏操作键。
- 本项目不会尝试绕过 VAC、反作弊或服务器限制。

## sb6657 接口约定

程序只调用公开的 `GET /machine/getRandOne` 随机烂梗接口，并解析其 JSON 响应。第三方客户端不携带 sb6657 官网专用的来源统计请求头。后端地址为应用内固定配置，设置界面不提供修改入口。

## 开发

要求：

- Windows
- [Godot 4.6.1 .NET](https://godotengine.org/)
- .NET 8 SDK

使用 Godot .NET 编辑器打开项目目录即可运行。命令行构建：

```powershell
dotnet restore .\Lanmian.csproj
dotnet build .\Lanmian.csproj --configuration Release --no-restore
```

`run.cmd` 面向已经在项目目录中准备好 `.dotnet` 和 `.godot-sdk` 本地工具的开发工作区；这两个目录被 Git 忽略，普通源码克隆默认不包含它们。

## 发布

项目使用 Godot 4.6.1 .NET 和 .NET 8 导出 Windows x64 便携版。推送以 `v` 开头的标签会触发 GitHub Actions，自动生成 ZIP、SHA-256 校验文件和 GitHub Release；带连字符的版本（例如 `v0.1.0-beta.1`）会标记为预发布。

在正式打标签前，可以从 GitHub Actions 手动运行 `Release` 工作流做一次 dry run。手动运行只上传保留 14 天的构建 Artifact，不会创建 GitHub Release。

```powershell
git tag v0.1.0-beta.1
git push origin v0.1.0-beta.1
```

正式发布前应在未安装 Godot 和 .NET SDK 的干净 Windows x64 环境中验证 ZIP。

## 许可证

烂梗喵的源代码使用 [MIT License](LICENSE) 发布。MIT 许可证不授予 sb6657 服务、接口返回内容、CS2、Steam 或其他第三方内容的权利；这些内容分别受其提供方条款约束。
