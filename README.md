# AutoDayNight

**日出浅色，日落深色。** 一个轻量的 Windows 托盘程序，按你的所在地离线计算每天的日出、日落，切换系统与应用主题。

[English](README.en.md) · [下载发行版](https://github.com/LappIand/AutoDayNight/releases) · [报告问题](https://github.com/LappIand/AutoDayNight/issues)

## 功能

- **不限于石家庄**：首次启动选择城市预设，或填写任意经纬度。托盘「所在地设置…」可随时修改。
- **正确处理时区**：城市预设会选对应的 Windows 时区，也可跟随 Windows 时区；夏令时按事件发生时的偏移量处理。
- **每日离线计算**：内置 NOAA/Meeus 天文公式，无网络请求、定位上传、API Key 或在线服务依赖。
- **启动补同步**：登录自启后立即判断当前应为深色还是浅色；关机期间错过日出/日落，启动时会补齐。
- **尊重手动切换**：两次天文边界之间不会抢回手动设置；重新启用自动切换、唤醒或系统时间变化时同步。
- **极昼/极夜**：没有普通日出/日落时显示极地状态，极昼浅色、极夜深色，并寻找下一次边界。
- **黑太阳 / 白月牙**：配合浅色/深色任务栏；内置多尺寸图标。
- **低资源消耗**：每 30 秒检查一次，天文结果缓存，不联网。内存占用受系统与 DPI 环境影响，通常为几十 MB。
- **可选低电量提醒**：电量低于 20% 且未接电源时全屏询问，默认 60 秒无人回应后请求关机。**公开版首次安装默认关闭**，由用户主动开启。

## 安装（Windows 10 / 11）

1. 从 [Releases](https://github.com/LappIand/AutoDayNight/releases) 下载 `AutoDayNight-v1.0.0-windows.zip`，完整解压。
2. 双击 `install.cmd`：安装到 `%LOCALAPPDATA%\AutoDayNight`，设置当前用户登录自启，然后启动。
3. 首次运行会打开**所在地设置**：选城市或填经纬度，确认时区，预览今天的日出日落，点「保存并应用」。
4. 右下角托盘会出现太阳/月牙。图标可能藏在托盘展开区域。

无需管理员权限，也无需安装 .NET SDK；使用 Windows 自带的 .NET Framework 4.x。

**首次设置取消后**：程序仍在托盘，但日出日落自动切换保持等待状态，直到设置所在地。没有默认套用任何城市。

**升级**：退出程序或直接运行新包的 `install.cmd`，安装脚本会先停止已安装实例再更新，**不覆盖已有配置**。旧版石家庄用户的经纬度、自动开关、低电量开关会迁移保留。

## 不同地区怎么设置

右键托盘 → **所在地设置…**：

- **城市预设**：包含石家庄、北京、上海、广州、深圳、成都、乌鲁木齐、香港、台北，以及 Tokyo、Singapore、New Delhi、London、Berlin、New York、Los Angeles、Sydney、Auckland、Honolulu、Tromso。
- **任意地区**：选择「自定义经纬度」，填写地点名称、纬度、经度和时区。
- **纬度**：北纬为正、南纬为负（`-90..90`）；**经度**：东经为正、西经为负（`-180..180`）。
- **时区**：应与设置的所在地对应。若跟随 Windows，请确认系统时区正确；也可直接选择该城市对应的时区。
- **迁移/旅行**：更换城市后重新设置。程序不会根据 IP 或 GPS 自动追踪位置。

时刻按**所选所在地时区**显示，不固定北京时间。城市预设使用市中心近似经纬度，可手动调整。

## 托盘菜单

| 项目 | 作用 |
|---|---|
| 当前主题 / 当地日出日落 / 下次切换 / 电源 | 状态显示 |
| 所在地设置… | 修改经纬度与时区，保存后立即应用 |
| 立即切换为浅色/深色 | 手动切换 |
| 自动切换（日出/日落） | 暂停/恢复；恢复时立即同步 |
| 开机自启动 | 当前用户登录自启开关 |
| 低电量关机提醒 | 可选功能开关，默认关闭 |
| 退出 | 结束常驻；下次启动会重新同步 |

地区设置与主要菜单自动使用中文或英文，部分诊断日志为中英混合。

## 低电量提醒（主动启用）

- 放电时电量 `< low_batt_percent`，默认阈值 20%，出现置顶全屏询问。
- 点击「立即关机」请求关机；点击「取消，不关机」、按 Esc 或关闭窗口视为取消。
- 倒计时结束仍无人回应时执行 `shutdown.exe /s /t 0`；未保存的工作可能阻止 Windows 正常关机，程序不使用强制结束应用参数。
- 倒计时期间接上电源、充回阈值或关闭功能会取消提醒。
- 一次低电量事件只提醒一次，接电或充回阈值后重新启用下一次提醒。无电池设备静默。
- 测试方式：`AutoDayNight.exe --test-lowbatt 15 8`，测试分支始终仅关闭弹窗，**不会真实关机**。

## 配置与隐私

实际配置为 `%LOCALAPPDATA%\AutoDayNight\config.ini`；日志为同目录 `log.txt`（超过约 200 KB 截断）。源码目录内 `config.ini` 只是新安装模板，不是任何人的实际位置。

```ini
location_configured=1
city=London
lat=51.5074
lon=-0.1278
time_zone=GMT Standard Time
auto=1
sunrise_offset_min=0
sunset_offset_min=0
low_batt_alert=0
low_batt_percent=20
low_batt_grace_sec=60
```

`time_zone=` 留空表示跟随 Windows。手动编辑后重启；托盘设置会立即生效。两个 offset 为分钟，**正数延后、负数提前**，范围 `-180..180`。阈值范围 `1..99`，倒计时范围 `10..3600` 秒。

不收集遥测，不上传地点、电量或日志。报告问题时只分享必要信息，先清理本地日志里的个人内容。`AUTODAYNIGHT_DATA_DIR` 环境变量只用于指定其他数据目录及隔离测试，默认无需设置。

## 构建与测试

```powershell
.\build.cmd
.\build.cmd -Test
.\build.cmd -Test -Package
```

编译器自动从 Windows 的 Framework64/Framework 目录查找。源码兼容 C# 5，无第三方依赖。`-Test` 在隔离数据目录中验证天文计算、DST、极地、日期变更线、配置迁移和窗口构造，**不修改真实主题、注册表或关机状态**。

生成程序为根目录 `AutoDayNight.exe`；发行 ZIP 与 SHA-256 校验文件在 `artifacts/`。构建产物不提交到 Git，安装包发布在 Releases。

```text
AutoDayNight.exe --status
AutoDayNight.exe --test-calc
AutoDayNight.exe --set dark
AutoDayNight.exe --location
AutoDayNight.exe --dump-icons OUTPUT_DIR
AutoDayNight.exe --calc 2026-03-08 40.7128 -74.0060 "Eastern Standard Time"
```

`--calc` 是纯计算，不改用户配置。CLI 修改所在地后请重启已有托盘实例。图形程序输出同时保存到 `%TEMP%\AutoDayNight.out`。

GitHub Actions 自动构建示例见 [docs/windows-ci.example.yml](docs/windows-ci.example.yml)。当前维护者 OAuth 身份未包含 workflow scope，因此示例随源码提供，未假称已启用线上自动构建。

## 算法与限制

太阳位置使用 NOAA/Meeus 公式，在 UTC 计算事件后转换到所在地时区。采用地平线高度 -0.833°（大气折射和太阳视半径）；不建模海拔、山地遮挡或实时天气，通常足够用于主题切换，但不用于导航或精密观测。高纬度边界附近误差可能增大。

30 秒检查间隔意味着在线切换通常发生在边界后 0–30 秒；登录自启还受 Windows 调度延迟影响。退出后不再执行定时切换，没有看门狗。锁屏界面不属于本程序主题控制范围；部分旧应用只在重启后读取主题。

公式参考：[NOAA Solar Calculation Details](https://gml.noaa.gov/grad/solcalc/calcdetails.html)。项目为独立实现，与 NOAA 无隶属关系。

## 卸载与开源

双击 `uninstall.cmd`：停止已安装程序、移除当前用户自启项、删除安装目录（包括配置和日志）。不会回滚当前主题。

许可证：[MIT](LICENSE)。欢迎提交不同地区的测试数据、界面改进与修复；见 [CONTRIBUTING.md](CONTRIBUTING.md)。
