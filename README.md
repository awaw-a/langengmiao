# 烂梗喵

一个 Windows 桌面工具：从 [sb6657](https://sb6657.cn) 获取随机烂梗，并通过可配置按键在 CS2 中发送。

## 当前功能

- Godot 4 C# 图形化界面
- 调用 `GET /machine/getRandOne` 获取随机烂梗
- 维护少量预取队列，降低按键发送延迟
- 自动检测 Steam 与 CS2 安装目录，也可在界面中手动选择
- 在界面中点击并直接按键来选择游戏内发送键，默认 `F8`
- 可选择全体聊天或队内聊天
- 通过 CS2 原生 `say` / `say_team` CFG 发送
- 自动备份并以托管区方式更新 `autoexec.cfg`
- 发送后在后台补充下一条烂梗
- 发送历史、接口测试、配置持久化

## 开发环境

- Godot .NET 4.6.x
- .NET 8 SDK
- Windows

使用 Godot .NET 编辑器打开项目目录即可运行。也可以使用对应版本的 `dotnet` / Godot 构建命令。

当前工作区已经包含本地开发工具时，可以直接双击 `run.cmd`。启动脚本会自动设置项目内的 .NET 8 路径和可写数据目录，避免直接启动 Godot 时因运行时或日志目录不可用而崩溃。

## 发送方式说明

本项目不注入 CS2、不读取游戏进程内存，也不使用 `-allow_third_party_software`。发送功能由 CS2 自己执行 `exec lanmian_send`，因此不会打开聊天栏、占用剪贴板或模拟 Y/U/Enter。

首次使用：

1. 启动烂梗喵，确认界面已经检测到 CS2 路径；自动检测失败时，点击“手动选择目录”并选择 CS2 安装目录。误选到其下的 `game`、`csgo` 或 `cfg` 目录时，程序也会自动识别安装根目录。
2. 点击发送键按钮，再按下想使用的键；无需手动输入键名。
3. 关闭 CS2 后点击“应用 CFG 绑定”，程序会安全更新当前 Steam 用户的按键配置。
4. 启动 CS2，新绑定会直接生效，无需在游戏控制台执行命令。
5. 在游戏中按选择的发送键，CS2 会直接发送当前烂梗。

“一键删除烂梗喵 CFG”只会移除命令严格等于 `exec lanmian_send` 的用户按键、烂梗喵写入 `autoexec.cfg` 的托管区，以及带有烂梗喵标记的 `lanmian_bootstrap.cfg`、`lanmian_send.cfg`；不会删除其他 CFG 或按键。应用会记录新键原来的命令，并在删除时恢复。各配置的 `.lanmian.bak` 备份不会被删除。

自动化聊天可能受到 Steam 或服务器规则限制，请先在离线、私人或你确认允许的环境中测试。

## sb6657 接口约定

第三方客户端不携带 sb6657 官网专用的来源统计请求头。程序只使用公开随机烂梗接口的 JSON 响应。

## 发布

项目使用 Godot 4.6.1 .NET 和 .NET 8 导出 Windows x64 便携版。推送以 `v` 开头的标签会触发 GitHub Actions，自动生成 ZIP、SHA-256 校验文件和 GitHub Release；带连字符的版本（例如 `v0.1.0-beta.1`）会标记为预发布。

在正式打标签前，可以从 GitHub Actions 手动运行 `Release` 工作流做一次 dry run。手动运行只上传 14 天有效的构建 Artifact，不会创建 GitHub Release。

```powershell
git tag v0.1.0-beta.1
git push origin v0.1.0-beta.1
```

正式发布前应先在未安装 Godot 和 .NET SDK 的干净 Windows 环境中验证 ZIP。应用和删除 CS2 按键绑定前必须关闭 CS2。
