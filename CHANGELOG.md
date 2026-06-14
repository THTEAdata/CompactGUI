# Changelog

## [1.1.0] - 2026-06-13

### ✨ 新增功能

#### 📊 文件类型分析面板（Results 页）
- 压缩完成后，在结果页新增可折叠的「文件类型分析」面板
- 按扩展名分组，展示每种类型的：文件数、压缩前大小、压缩后大小、节省空间百分比
- 帮助用户快速了解哪些文件类型占用了多少空间

#### 🏆 Top 文件排行榜（Results 页）
- 压缩完成后，在结果页新增可折叠的「Top 最大文件」面板
- 列出压缩包中最大的 15 个文件（文件名、原始大小、压缩模式）
- 方便用户定位大文件

#### 📥 CSV 导出（Results 页）
- 结果页右下角新增「CSV 导出」按钮
- 一键导出完整压缩分析报告，包含：文件名、扩展名、原始大小、压缩大小、压缩率、压缩模式
- 便于用户后续进行数据分析或存档

#### 📈 实时性能监控仪表盘（压缩进行中）
- 压缩进行时自动显示性能卡片组：
  - **CPU**：CompactGUI 进程 CPU 使用率（百分比），数字颜色随负载绿→黄→红变化
  - **内存**：进程物理内存占用（MB）
  - **磁盘读**：数据读取速率（MB/s）
  - **磁盘写**：压缩后数据写入速率（MB/s）
- 使用 `DispatcherTimer` 每秒采样，避免 WPF 跨线程问题
- 压缩结束后自动隐藏，不干扰界面

#### 🎮 功能开关（设置页）
- 设置页新增「功能开关」分类
- 可通过 CheckBox 独立开关以下功能（默认全部开启）：
  - 📊 文件类型分析
  - 🏆 Top 文件排行榜
  - 📥 CSV 导出
  - 📈 实时性能监控
- 开关即时生效，无需重启
- 关闭时对应 UI 完全隐藏（不占布局空间）
- 性能监控关闭后自动停止采样定时器，节省系统资源

#### 👆 监控页文件夹路径跳转
- 文件夹监控卡片中的路径文字支持鼠标点击
- 鼠标悬停变手型，点击自动调用资源管理器打开对应目录
- 使用 `explorer.exe` 启动，兼容 Windows 环境

### 🐛 Bug 修复

#### 1. Analyser.cs — `GetPoorlyCompressedExtensions()` 空引用崩溃
- **场景**：未执行分析时调用扩展压缩率统计
- **原因**：`_analysedFileDetails` 为 null 时直接 `.AsParallel()` 导致 NullReferenceException
- **修复**：前置 null 检查

#### 2. Analyser.cs — `AnalyseFile()` 异常捕获不全
- **场景**：分析系统保护文件或路径过长的文件
- **原因**：仅捕获 `IOException`，遗漏 `UnauthorizedAccessException`、`SecurityException`、`PathTooLongException`
- **修复**：补充多种异常捕获 + 兜底通用异常处理

#### 3. CompressableFolder.vb — `CompressionRatio` 除零崩溃
- **场景**：文件夹为空（UncompressedBytes = 0）时显示压缩比率
- **原因**：`CompressedBytes / UncompressedBytes` 分母为零触发 `DivideByZeroException`
- **修复**：前置检查 `If UncompressedBytes = 0 Then Return 0`

#### 4. CompressableFolder.vb — `GlobalPoorlyCompressedFileCount` 多层空引用
- **场景**：后台监控扫描完成但前端尚未初始化完成时访问
- **原因**：`Application.GetService` 返回 null + `FileInfo(null)` 参数异常
- **修复**：多层 null 防护 + Try/Catch 包裹

#### 5. Compactor.cs — `BuildWorkingFilesList()` null 引用
- **场景**：分析结果未生成时尝试构建压缩工作列表
- **原因**：`analysedFiles` 或其中 `FileInfo` 为 null
- **修复**：显式判 null + 返回空列表

#### 6. SteamFolder.vb — `WikiPoorlyCompressedFilesCount` FileInfo(null) 崩溃
- **场景**：Steam 文件夹分析相关统计
- **原因**：`New FileInfo(Nothing)` 触发 `ArgumentNullException`
- **修复**：前置 null 检查 + Try/Catch 包裹

#### 7. CompressableFolderService.vb — `GetEstimatedCompression()` 异常捕获不全 + 数据重复
- **场景**：估算压缩率时异常未完全捕获，多次调用时追加重复数据
- **原因**：仅捕获 `AggregateException`，漏了 `TaskCanceledException` 等；未在每次调用前 `Clear` 结果集
- **修复**：全异常捕获 + null 检查 + 每次调用前自动重置

### 🛡️ 空引用加固
- `FolderViewModel.vb` 中多个属性（`TotalCompressedFiles`、`TotalFiles`、`CompressionDisplayLevel`、`DominantCompressionMode`、`DisplayedFolderAfterSize`）增加 null 安全前置检查

### 🔧 项目结构
- 保持原版 VB.NET + C# 混合项目结构不变
- 所有修改均在原文件基础上进行，兼容 `.slnx` 解决方案文件和 MSBuild 构建系统

### ⚠️ 已知限制
- 文件类型分析和 Top 文件面板仅在压缩完成后（Results 状态）可用
- 性能监控依赖 `System.Diagnostics.Process` 计数器，在极高 IO 压力下采样可能略有延迟（1 秒间隔）
