
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Windows.Threading

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports CompactGUI.Core
Imports CompactGUI.Core.Settings

Imports Wpf.Ui.Controls


Public NotInheritable Class FolderViewModel : Inherits ObservableObject : Implements IDisposable

    <ObservableProperty>
    Private _Folder As CompressableFolder

    <ObservableProperty>
    Private _CompressionProgress As Integer

    <ObservableProperty>
    Private _CompressionProgressFile As String

    <ObservableProperty>
    Private _AlwaysShowDetailsCompressionMode As Boolean = False

    Private ReadOnly _watcher As Watcher.Watcher
    Private ReadOnly _snackbarService As CustomSnackBarService
    Private ReadOnly _compressableFolderService As CompressableFolderService

    ' ==== 性能监控字段 ====
    Private _perfTimer As DispatcherTimer
    Private _prevCpuTime As TimeSpan
    Private _prevWallClock As DateTime
    Private _prevReadBytes As Long
    Private _prevWriteBytes As Long
    Private _lastProcessedBytes As Long
    Private ReadOnly _perfProcess As Process = Process.GetCurrentProcess()

    <ObservableProperty> Private _CpuPercent As Double = 0
    <ObservableProperty> Private _MemoryMB As Double = 0
    <ObservableProperty> Private _DiskReadMBs As Double = 0
    <ObservableProperty> Private _DiskWriteMBs As Double = 0

    Public Sub New(folder As CompressableFolder, watcher As Watcher.Watcher, snackbarService As CustomSnackBarService, compressableFolderService As CompressableFolderService)
        Me.Folder = folder
        _watcher = watcher
        _snackbarService = snackbarService
        _compressableFolderService = compressableFolderService
        AddHandler folder.PropertyChanged, AddressOf OnFolderPropertyChanged
        AddHandler folder.CompressionOptions.PropertyChanged, AddressOf OnFolderCompressionOptionsPropertyChanged
        AddHandler Application.GetService(Of Core.Settings.ISettingsService).AppSettings.PropertyChanged, AddressOf OnAppSettingsPropertyChanged
    End Sub

    Private Sub OnAppSettingsPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName Is NameOf(Core.Settings.Settings.AlwaysShowDetailedCompressionMode) Then
            AlwaysShowDetailsCompressionMode = Application.GetService(Of Core.Settings.ISettingsService).AppSettings.AlwaysShowDetailedCompressionMode
        End If
        ' 性能监控开关：关闭时立即停止计时
        If e.PropertyName Is NameOf(Core.Settings.Settings.EnablePerformanceMonitor) Then
            UpdatePerformanceTimerState()
        End If
    End Sub

    ''' <summary>
    ''' 暴露 AppSettings 给 XAML 绑定（用于功能开关等）
    ''' </summary>
    Public ReadOnly Property AppSettings As Core.Settings.Settings
        Get
            Return Application.GetService(Of Core.Settings.ISettingsService).AppSettings
        End Get
    End Property

    Public ReadOnly Property IsAnalysing As Boolean
        Get
            Return Folder?.FolderActionState = ActionState.Analysing
        End Get
    End Property

    Public ReadOnly Property IsIdle As Boolean
        Get
            Return Folder?.FolderActionState = ActionState.Idle
        End Get
    End Property

    Public ReadOnly Property IsCompressing As Boolean
        Get
            Dim s = Folder?.FolderActionState
            Return s = ActionState.Working OrElse s = ActionState.Paused
        End Get
    End Property

    Public ReadOnly Property IsResults As Boolean
        Get
            Return Folder?.FolderActionState = ActionState.Results
        End Get
    End Property

    Public ReadOnly Property IsNotResultsOrAnalysing As Boolean
        Get
            Return Folder?.FolderActionState <> ActionState.Results AndAlso Not IsAnalysing
        End Get
    End Property

    Public ReadOnly Property CompressionDisplayLevel As String
        Get
            If Folder Is Nothing OrElse Folder.AnalysisResults Is Nothing OrElse
            Not Folder.AnalysisResults.Any(Function(x) x.CompressionMode <> Core.WOFCompressionAlgorithm.NO_COMPRESSION) Then
                Return LanguageHelper.GetString("Status_NotCompressed") 'Not Compressed
            End If
            Return LanguageHelper.GetString("Status_Compressed") 'Compressed
        End Get
    End Property

    Public ReadOnly Property TotalCompressedFiles As Integer
        Get
            If Folder Is Nothing OrElse Folder.AnalysisResults Is Nothing Then Return 0
            Return Folder.AnalysisResults.Where(Function(x) x.CompressionMode <> Core.WOFCompressionAlgorithm.NO_COMPRESSION).Count()
        End Get
    End Property

    Public ReadOnly Property TotalFiles As Integer
        Get
            If Folder Is Nothing OrElse Folder.AnalysisResults Is Nothing Then Return 0
            Return Folder.AnalysisResults.Count
        End Get
    End Property

    Public ReadOnly Property DominantCompressionMode As Core.WOFCompressionAlgorithm
        Get
            If Folder Is Nothing OrElse Folder.AnalysisResults Is Nothing Then Return Core.WOFCompressionAlgorithm.NO_COMPRESSION
            Return Folder.AnalysisResults _
            .Where(Function(x) x.CompressionMode <> Core.WOFCompressionAlgorithm.NO_COMPRESSION) _
            .GroupBy(Function(x) x.CompressionMode) _
            .OrderByDescending(Function(g) g.Count()) _
            .Select(Function(g) g.Key) _
            .FirstOrDefault()
        End Get
    End Property

    Public ReadOnly Property DisplayedFolderAfterSize As Long
        Get
            If Folder Is Nothing Then Return 0
            If TypeOf (Folder) Is SteamFolder AndAlso (Folder.FolderActionState = ActionState.Idle OrElse Folder.FolderActionState = ActionState.Working) Then
                Dim working = CType(Folder, SteamFolder)
                If working.WikiCompressionResults Is Nothing Then Return Folder.CompressedBytes
                Select Case working.CompressionOptions.SelectedCompressionMode
                    Case Core.CompressionMode.XPRESS4K
                        Return CLng(working.WikiCompressionResults.XPress4K?.CompressionPercent / 100 * working.UncompressedBytes)
                    Case Core.CompressionMode.XPRESS8K
                        Return CLng(working.WikiCompressionResults.XPress8K?.CompressionPercent / 100 * working.UncompressedBytes)
                    Case Core.CompressionMode.XPRESS16K
                        Return CLng(working.WikiCompressionResults.XPress16K?.CompressionPercent / 100 * working.UncompressedBytes)
                    Case Core.CompressionMode.LZX
                        Return CLng(working.WikiCompressionResults.LZX?.CompressionPercent / 100 * working.UncompressedBytes)
                End Select


            End If
            Return Folder.CompressedBytes
        End Get
    End Property


    Public ReadOnly Property IsSteamIDVisible
        Get
            Return TypeOf Folder Is SteamFolder
        End Get
    End Property

    Private Sub OnFolderCompressionOptionsPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        Dim compressionOptions = CType(sender, CompressionOptions)
        If e.PropertyName = NameOf(compressionOptions.SelectedCompressionMode) Then
            OnPropertyChanged(NameOf(DisplayedFolderAfterSize))
        End If
    End Sub

    Private Sub OnFolderPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(Folder.FolderActionState) Then
            OnPropertyChanged(NameOf(IsAnalysing))
            OnPropertyChanged(NameOf(IsIdle))
            OnPropertyChanged(NameOf(IsCompressing))
            OnPropertyChanged(NameOf(IsResults))
            OnPropertyChanged(NameOf(IsNotResultsOrAnalysing))
            OnPropertyChanged(NameOf(CompressionDisplayLevel))
            OnPropertyChanged(NameOf(DisplayedFolderAfterSize))
            OnPropertyChanged(NameOf(TotalFiles))
            UpdatePerformanceTimerState()

        ElseIf e.PropertyName = NameOf(Folder.CompressionProgress) Then
            CompressionProgress = Folder.CompressionProgress.ProgressPercent
            CompressionProgressFile = Folder.CompressionProgress.FileName.Replace(Folder.FolderName, "")

        End If
    End Sub

    ' ==== 性能监控定时器管理 ====
    Private Sub StartPerformanceMonitor()
        If _perfTimer Is Nothing Then
            _perfTimer = New DispatcherTimer()
            _perfTimer.Interval = TimeSpan.FromSeconds(1)
            AddHandler _perfTimer.Tick, AddressOf PerfTimerTick
        End If

        ' 重置快照基准
        _perfProcess.Refresh()
        _prevCpuTime = _perfProcess.TotalProcessorTime
        _prevWallClock = DateTime.UtcNow
        _lastProcessedBytes = 0
        OnPropertyChanged(NameOf(CpuPercent))
        OnPropertyChanged(NameOf(MemoryMB))
        OnPropertyChanged(NameOf(DiskReadMBs))
        OnPropertyChanged(NameOf(DiskWriteMBs))

        _perfTimer.Start()
    End Sub

    Private Sub StopPerformanceMonitor()
        If _perfTimer IsNot Nothing Then
            _perfTimer.Stop()
        End If
        CpuPercent = 0
        MemoryMB = 0
        DiskReadMBs = 0
        DiskWriteMBs = 0
    End Sub

    Private Sub PerfTimerTick(sender As Object, e As EventArgs)
        Try
            _perfProcess.Refresh()

            ' CPU: (CPU时间差) / (CPU核心数 × 真实时间差) × 100
            Dim nowCpu = _perfProcess.TotalProcessorTime
            Dim nowWall = DateTime.UtcNow
            Dim cpuDelta = (nowCpu - _prevCpuTime).TotalSeconds
            Dim wallDelta = (nowWall - _prevWallClock).TotalSeconds
            If wallDelta > 0 Then
                CpuPercent = Math.Round(cpuDelta / (Environment.ProcessorCount * wallDelta) * 100, 1)
            End If
            _prevCpuTime = nowCpu
            _prevWallClock = nowWall

            ' 内存: WorkingSet (字节 → MB)
            MemoryMB = Math.Round(_perfProcess.WorkingSet64 / 1024.0 / 1024.0, 1)

            ' 磁盘: 根据压缩进度变化估算读写速率
            If Folder IsNot Nothing Then
                Dim nowProcessed = CLng(Folder.UncompressedBytes * Folder.CompressionProgress.ProgressPercent / 100)
                Dim deltaBytes = nowProcessed - _lastProcessedBytes
                If deltaBytes > 0 AndAlso wallDelta > 0 Then
                    Dim mbs = deltaBytes / 1024.0 / 1024.0 / wallDelta
                    DiskReadMBs = Math.Round(mbs, 1)  ' 读 = 原始文件读取速度
                    DiskWriteMBs = Math.Round(mbs * CDbl(Folder.CompressionRatio), 1)  ' 写 ≈ 读 × 压缩比
                End If
                _lastProcessedBytes = nowProcessed
            End If

            ' 通知 UI 更新
            OnPropertyChanged(NameOf(CpuPercent))
            OnPropertyChanged(NameOf(MemoryMB))
            OnPropertyChanged(NameOf(DiskReadMBs))
            OnPropertyChanged(NameOf(DiskWriteMBs))
        Catch ex As Exception
            Debug.WriteLine($"性能监控采样异常: {ex.Message}")
        End Try
    End Sub

    Private Sub UpdatePerformanceTimerState()
        If Folder Is Nothing Then
            StopPerformanceMonitor()
            Return
        End If
        ' 检查功能开关：关闭时永不启动
        Dim perfEnabled = Application.GetService(Of Core.Settings.ISettingsService).AppSettings.EnablePerformanceMonitor
        If Not perfEnabled Then
            StopPerformanceMonitor()
            Return
        End If
        Select Case Folder.FolderActionState
            Case ActionState.Working, ActionState.Paused
                StartPerformanceMonitor()
            Case Else
                StopPerformanceMonitor()
        End Select
    End Sub


    <RelayCommand>
    Private Sub CompressAgain()
        Folder.FolderActionState = ActionState.Idle
    End Sub

    <RelayCommand>
    Private Async Function Uncompress() As Task
        Await _compressableFolderService.UncompressFolder(Folder)
        _watcher.UpdateWatched(Folder.FolderName, Folder.Analyser, False)
    End Function

    <RelayCommand>
    Private Sub ApplyToAll()
        Dim allFolders = Application.GetService(Of HomeViewModel)().Folders

        For Each fl In allFolders.Where(Function(f) f.FolderActionState <> ActionState.Analysing AndAlso f.FolderActionState <> ActionState.Working AndAlso f.FolderActionState <> ActionState.Paused)
            If fl IsNot Folder Then
                fl.CompressionOptions = Folder.CompressionOptions.Clone
                fl.FolderActionState = ActionState.Idle
            End If
        Next

        _snackbarService.ShowAppliedToAllFolders()
    End Sub

    <RelayCommand>
    Private Sub Pause()

        If Folder.FolderActionState = ActionState.Working Then
            Folder.Compressor?.Pause()
            Folder.FolderActionState = ActionState.Paused
        Else
            Folder.Compressor?.Resume()
            Folder.FolderActionState = ActionState.Working

        End If
    End Sub

    <RelayCommand>
    Private Sub Cancel()
        Folder.Compressor?.Cancel()
    End Sub

    <RelayCommand>
    Private Async Function SubmitToWiki() As Task

        SubmitToWikiCommand.NotifyCanExecuteChanged()

        Dim result = Await Application.GetService(Of IWikiService).SubmitToWiki(Folder.FolderName, Folder.AnalysisResults.ToList, Folder.PoorlyCompressedFiles, Folder.CompressionOptions.SelectedCompressionMode)

        Folder.IsFreshlyCompressed = False
        SubmitToWikiCommand.NotifyCanExecuteChanged()
    End Function
    Private Function CanSubmitToWiki() As Boolean
        Return TypeOf (Folder) _
            Is SteamFolder AndAlso
            Folder.IsFreshlyCompressed AndAlso
            Not Folder.CompressionOptions.SkipPoorlyCompressedFileTypes AndAlso
            Not Folder.CompressionOptions.SkipUserSubmittedFiletypes
    End Function






    ' ==== 新增：信息显示属性 ====

    ''' <summary>文件类型统计列表（按扩展名分组）</summary>
    Public ReadOnly Property FileTypeStats As List(Of FileTypeStat)
        Get
            If Folder?.Analyser Is Nothing Then Return New List(Of FileTypeStat)
            Return Folder.Analyser.GetFileTypeStats()
        End Get
    End Property

    ''' <summary>是否可显示文件类型分析</summary>
    Public ReadOnly Property HasFileTypeStats As Boolean
        Get
            Return FileTypeStats.Any()
        End Get
    End Property

    ''' <summary>Top N 最大文件</summary>
    Public ReadOnly Property TopFilesList As List(Of AnalysedFileDetails)
        Get
            If Folder?.Analyser Is Nothing Then Return New List(Of AnalysedFileDetails)
            Return Folder.Analyser.GetTopFiles(15)
        End Get
    End Property

    ''' <summary>压缩比描述文字</summary>
    Public ReadOnly Property CompressionRatioDescription As String
        Get
            Dim ratio = Folder.CompressionRatio
            If ratio = 0 Then Return "---"
            Dim saved = (1 - ratio) * 100
            Return $"节省 {saved:F1}% 空间"
        End Get
    End Property

    ''' <summary>已压缩的文件数量（用于实时仪表盘）</summary>
    Public ReadOnly Property TotalProcessedFiles As Integer
        Get
            If Folder?.AnalysisResults Is Nothing Then Return 0
            ' 计算压缩进度百分比对应已处理的文件数
            Dim total = Folder.AnalysisResults.Count
            If total = 0 Then Return 0
            Return CInt(total * Folder.CompressionProgress.ProgressPercent / 100)
        End Get
    End Property

    ''' <summary>实时压缩速度（用于仪表盘显示）</summary>
    Public ReadOnly Property RealTimeProgress As RealTimeProgressInfo
        Get
            Dim info As New RealTimeProgressInfo
            If Folder?.Compressor Is Nothing Then Return info
            info.TotalBytes = Folder.UncompressedBytes
            info.ProcessedBytes = CLng(Folder.UncompressedBytes * Folder.CompressionProgress.ProgressPercent / 100)
            info.TotalFiles = If(Folder.AnalysisResults?.Count, 0)
            info.CurrentFile = If(Folder.CompressionProgress.FileName, "")
            info.Elapsed = TimeSpan.Zero
            Return info
        End Get
    End Property

    ''' <summary>是否有分析结果可导出</summary>
    Public ReadOnly Property CanExport As Boolean
        Get
            Return If(Folder?.AnalysisResults?.Any(), False)
        End Get
    End Property

    <RelayCommand>
    Private Async Function ExportResultsCsv() As Task
        Dim folderPath = Folder.FolderName
        Dim safeName = New String(folderPath.Where(Function(c) Char.IsLetterOrDigit(c) OrElse c = "-"c OrElse c = "_"c).ToArray())
        If String.IsNullOrWhiteSpace(safeName) Then safeName = "export"
        Dim defaultName = $"CompactGUI_Analysis_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"

        ' 使用 WPF-UI 的保存对话框保存
        Dim sfd As New Microsoft.Win32.SaveFileDialog
        sfd.FileName = defaultName
        sfd.Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*"
        sfd.DefaultExt = ".csv"

        If sfd.ShowDialog() = True Then
            Try
                Using sw As New System.IO.StreamWriter(sfd.FileName, False, System.Text.Encoding.UTF8)
                    ' 写入头部
                    Await sw.WriteLineAsync("文件名,扩展名,原始大小(KB),压缩后大小(KB),压缩率,压缩模式,节省比例")
                    ' 写入每行
                    For Each file In Folder.AnalysisResults.OrderByDescending(Function(f) f.UncompressedSize)
                        Dim ext = System.IO.Path.GetExtension(file.FileName)
                        Dim ratio = If(file.UncompressedSize > 0, Math.Round(CDbl(file.CompressedSize) / file.UncompressedSize, 4), 0)
                        Dim saved = If(file.UncompressedSize > 0, Math.Round((1 - CDbl(file.CompressedSize) / file.UncompressedSize) * 100, 2), 0)
                        Await sw.WriteLineAsync(
                            $"""{file.FileName}"",""{ext}"",{file.UncompressedSize / 1024:F2},{file.CompressedSize / 1024:F2},{ratio},{file.CompressionMode},{saved}%")
                    Next
                End Using
                _snackbarService.Show("分析结果已导出到:" & vbCrLf & sfd.FileName)
            Catch ex As Exception
                _snackbarService.Show("导出失败: " & ex.Message)
            End Try
        End If
    End Function


    Public Sub Dispose() Implements IDisposable.Dispose
        RemoveHandler Folder.PropertyChanged, AddressOf OnFolderPropertyChanged
        RemoveHandler Folder.CompressionOptions.PropertyChanged, AddressOf OnFolderCompressionOptionsPropertyChanged
    End Sub



End Class

